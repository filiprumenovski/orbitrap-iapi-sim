use anyhow::Result;
use clap::Parser;
use tonic::transport::Server;
use tracing::{info, Level};
use tracing_subscriber::FmtSubscriber;

mod proto;
mod service;
mod simulator;

use service::SimulatorServiceImpl;

/// LC-MS Orbitrap Simulator
///
/// A gRPC server that simulates an Orbitrap mass spectrometer
/// for development and testing of proteomics software.
#[derive(Parser, Debug)]
#[command(author, version, about, long_about = None)]
struct Args {
    /// Port to listen on
    #[arg(short, long, default_value_t = 31417)]
    port: u16,

    /// Host address to bind to
    #[arg(short = 'H', long, default_value = "0.0.0.0")]
    host: String,

    /// Log level (trace, debug, info, warn, error)
    #[arg(short, long, default_value = "info")]
    log_level: String,

    /// Instrument name to report
    #[arg(long, default_value = "Simulated Orbitrap Exploris 480")]
    instrument_name: String,

    /// Instrument ID to report
    #[arg(long, default_value = "SIM-001")]
    instrument_id: String,
}

#[tokio::main]
async fn main() -> Result<()> {
    let args = Args::parse();

    // Initialize logging
    let log_level = match args.log_level.to_lowercase().as_str() {
        "trace" => Level::TRACE,
        "debug" => Level::DEBUG,
        "info" => Level::INFO,
        "warn" => Level::WARN,
        "error" => Level::ERROR,
        _ => Level::INFO,
    };

    let subscriber = FmtSubscriber::builder()
        .with_max_level(log_level)
        .with_target(true)
        .with_thread_ids(true)
        .with_file(true)
        .with_line_number(true)
        .finish();

    tracing::subscriber::set_global_default(subscriber)
        .expect("Failed to set tracing subscriber");

    // Create the simulator service
    let service = SimulatorServiceImpl::new(
        args.instrument_name.clone(),
        args.instrument_id.clone(),
    );

    let addr = format!("{}:{}", args.host, args.port).parse()?;

    info!("Starting LC-MS Simulator gRPC server");
    info!("  Instrument: {}", args.instrument_name);
    info!("  ID: {}", args.instrument_id);
    info!("  Listening on: {}", addr);

    Server::builder()
        .add_service(proto::simulator_service_server::SimulatorServiceServer::new(service))
        .serve(addr)
        .await?;

    Ok(())
}
