use std::pin::Pin;
use std::sync::atomic::{AtomicI64, AtomicU8, Ordering};
use std::sync::Arc;
use std::time::Duration;

use tokio::sync::{broadcast, Mutex};
use tokio_stream::wrappers::BroadcastStream;
use tokio_stream::Stream;
use tonic::{Request, Response, Status};
use tracing::{info, warn};

use crate::proto::*;
use crate::simulator::ScanGenerator;

/// gRPC service implementation for the LC-MS simulator
pub struct SimulatorServiceImpl {
    instrument_name: String,
    instrument_id: String,
    state: Arc<AtomicU8>,
    scan_count: Arc<AtomicI64>,
    scan_sender: broadcast::Sender<ScanMessage>,
    generator: Arc<Mutex<ScanGenerator>>,
    session_id: Arc<Mutex<Option<String>>>,
}

impl SimulatorServiceImpl {
    pub fn new(instrument_name: String, instrument_id: String) -> Self {
        let (scan_sender, _) = broadcast::channel(1000);

        Self {
            instrument_name,
            instrument_id,
            state: Arc::new(AtomicU8::new(AcquisitionState::Idle as u8)),
            scan_count: Arc::new(AtomicI64::new(0)),
            scan_sender,
            generator: Arc::new(Mutex::new(ScanGenerator::new())),
            session_id: Arc::new(Mutex::new(None)),
        }
    }

    fn get_state(&self) -> AcquisitionState {
        match self.state.load(Ordering::SeqCst) {
            0 => AcquisitionState::Idle,
            1 => AcquisitionState::Starting,
            2 => AcquisitionState::Acquiring,
            3 => AcquisitionState::Paused,
            4 => AcquisitionState::Stopping,
            5 => AcquisitionState::Completed,
            6 => AcquisitionState::Faulted,
            _ => AcquisitionState::Idle,
        }
    }

    fn set_state(&self, state: AcquisitionState) {
        self.state.store(state as u8, Ordering::SeqCst);
    }

    async fn run_acquisition(
        &self,
        params: Option<SimulationParameters>,
        max_scans: Option<i32>,
        max_duration_seconds: Option<f64>,
    ) {
        let params = params.unwrap_or_default();
        let scan_rate = if params.scan_rate > 0.0 { params.scan_rate } else { 2.0 };
        let ms2_per_ms1 = if params.ms2_per_ms1 > 0 { params.ms2_per_ms1 } else { 4 };
        let delay_ms = (1000.0 / scan_rate) as u64;

        let start_time = std::time::Instant::now();
        let mut scans_generated = 0i64;

        info!(
            "Starting acquisition: scan_rate={}, ms2_per_ms1={}",
            scan_rate, ms2_per_ms1
        );

        self.set_state(AcquisitionState::Acquiring);

        loop {
            // Check termination conditions
            if self.get_state() == AcquisitionState::Stopping {
                break;
            }

            if let Some(max) = max_scans {
                if scans_generated >= max as i64 {
                    break;
                }
            }

            if let Some(max_secs) = max_duration_seconds {
                if start_time.elapsed().as_secs_f64() >= max_secs {
                    break;
                }
            }

            // Generate MS1 scan
            let ms1_scan = {
                let mut gen = self.generator.lock().await;
                gen.generate_ms1()
            };

            if self.scan_sender.send(ms1_scan.clone()).is_err() {
                // No receivers, but that's OK
            }
            scans_generated += 1;
            self.scan_count.fetch_add(1, Ordering::SeqCst);

            // Generate MS2 scans
            for _ in 0..ms2_per_ms1 {
                if self.get_state() == AcquisitionState::Stopping {
                    break;
                }

                if let Some(max) = max_scans {
                    if scans_generated >= max as i64 {
                        break;
                    }
                }

                let ms2_scan = {
                    let mut gen = self.generator.lock().await;
                    let (precursor_mz, precursor_int) = gen.select_precursor(&ms1_scan);
                    gen.generate_ms2(precursor_mz, precursor_int)
                };

                if self.scan_sender.send(ms2_scan).is_err() {
                    // No receivers
                }
                scans_generated += 1;
                self.scan_count.fetch_add(1, Ordering::SeqCst);

                // Small delay between MS2 scans
                tokio::time::sleep(Duration::from_millis(delay_ms / (ms2_per_ms1 as u64 + 1))).await;
            }

            // Delay for next cycle
            tokio::time::sleep(Duration::from_millis(delay_ms)).await;
        }

        info!("Acquisition complete: {} scans generated", scans_generated);
        self.set_state(AcquisitionState::Completed);
    }
}

#[tonic::async_trait]
impl simulator_service_server::SimulatorService for SimulatorServiceImpl {
    type StreamScansStream = Pin<Box<dyn Stream<Item = Result<ScanMessage, Status>> + Send>>;

    async fn stream_scans(
        &self,
        request: Request<StreamScansRequest>,
    ) -> Result<Response<Self::StreamScansStream>, Status> {
        let _req = request.into_inner();
        let receiver = self.scan_sender.subscribe();

        let stream = BroadcastStream::new(receiver).filter_map(|result| {
            match result {
                Ok(scan) => Some(Ok(scan)),
                Err(e) => {
                    warn!("Broadcast error: {:?}", e);
                    None
                }
            }
        });

        Ok(Response::new(Box::pin(stream)))
    }

    async fn get_status(
        &self,
        _request: Request<GetStatusRequest>,
    ) -> Result<Response<StatusResponse>, Status> {
        let session_id = self.session_id.lock().await.clone().unwrap_or_default();

        Ok(Response::new(StatusResponse {
            state: self.get_state() as i32,
            scan_count: self.scan_count.load(Ordering::SeqCst),
            current_retention_time: 0.0, // Could track this
            session_id,
            error_message: String::new(),
        }))
    }

    async fn start_acquisition(
        &self,
        request: Request<StartAcquisitionRequest>,
    ) -> Result<Response<StartAcquisitionResponse>, Status> {
        let current_state = self.get_state();
        if current_state != AcquisitionState::Idle && current_state != AcquisitionState::Completed {
            return Ok(Response::new(StartAcquisitionResponse {
                success: false,
                session_id: String::new(),
                error_message: format!("Cannot start acquisition in state {:?}", current_state),
            }));
        }

        let req = request.into_inner();
        let session_id = uuid::Uuid::new_v4().to_string()[..8].to_string();

        *self.session_id.lock().await = Some(session_id.clone());
        self.scan_count.store(0, Ordering::SeqCst);
        self.set_state(AcquisitionState::Starting);

        // Reset generator
        *self.generator.lock().await = ScanGenerator::new();

        // Clone what we need for the async task
        let self_clone = SimulatorServiceImpl {
            instrument_name: self.instrument_name.clone(),
            instrument_id: self.instrument_id.clone(),
            state: Arc::clone(&self.state),
            scan_count: Arc::clone(&self.scan_count),
            scan_sender: self.scan_sender.clone(),
            generator: Arc::clone(&self.generator),
            session_id: Arc::clone(&self.session_id),
        };

        let max_scans = req.max_scans;
        let max_duration = req.max_duration_seconds;
        let params = req.simulation;

        tokio::spawn(async move {
            self_clone.run_acquisition(params, max_scans, max_duration).await;
        });

        info!("Started acquisition session: {}", session_id);

        Ok(Response::new(StartAcquisitionResponse {
            success: true,
            session_id,
            error_message: String::new(),
        }))
    }

    async fn stop_acquisition(
        &self,
        _request: Request<StopAcquisitionRequest>,
    ) -> Result<Response<StopAcquisitionResponse>, Status> {
        self.set_state(AcquisitionState::Stopping);

        let final_count = self.scan_count.load(Ordering::SeqCst);

        Ok(Response::new(StopAcquisitionResponse {
            success: true,
            final_scan_count: final_count,
            error_message: String::new(),
        }))
    }

    async fn pause_acquisition(
        &self,
        _request: Request<PauseAcquisitionRequest>,
    ) -> Result<Response<PauseAcquisitionResponse>, Status> {
        Ok(Response::new(PauseAcquisitionResponse {
            success: false,
            error_message: "Pause not implemented in simulator".to_string(),
        }))
    }

    async fn resume_acquisition(
        &self,
        _request: Request<ResumeAcquisitionRequest>,
    ) -> Result<Response<ResumeAcquisitionResponse>, Status> {
        Ok(Response::new(ResumeAcquisitionResponse {
            success: false,
            error_message: "Resume not implemented in simulator".to_string(),
        }))
    }

    async fn get_instrument_info(
        &self,
        _request: Request<GetInstrumentInfoRequest>,
    ) -> Result<Response<InstrumentInfoResponse>, Status> {
        Ok(Response::new(InstrumentInfoResponse {
            instrument_name: self.instrument_name.clone(),
            instrument_id: self.instrument_id.clone(),
            model: "Orbitrap Exploris 480".to_string(),
            serial_number: self.instrument_id.clone(),
            firmware_version: "1.0.0".to_string(),
            simulator_version: env!("CARGO_PKG_VERSION").to_string(),
            supported_analyzers: vec!["Orbitrap".to_string()],
            supported_fragmentation_types: vec![
                FragmentationType::Hcd as i32,
                FragmentationType::Cid as i32,
            ],
            max_resolution: 480000.0,
            min_mz: 50.0,
            max_mz: 6000.0,
        }))
    }
}

impl Default for SimulationParameters {
    fn default() -> Self {
        Self {
            scan_rate: 2.0,
            ms2_per_ms1: 4,
            min_mz: 200.0,
            max_mz: 2000.0,
            resolution: 120000.0,
            noise_level: 0.01,
            random_seed: 0,
        }
    }
}
