use rand::Rng;
use rand::SeedableRng;
use rand::rngs::StdRng;
use rand_distr::{Distribution, Normal};
use std::time::{SystemTime, UNIX_EPOCH};

use crate::proto::{FragmentationType, Polarity, ScanMessage};

/// Generates realistic-looking mass spectrometry scans
pub struct ScanGenerator {
    scan_number: i32,
    retention_time: f64,
    random: StdRng,
}

impl ScanGenerator {
    pub fn new() -> Self {
        Self {
            scan_number: 0,
            retention_time: 0.0,
            random: StdRng::from_entropy(),
        }
    }

    /// Generates an MS1 (survey) scan
    pub fn generate_ms1(&mut self, min_mz: f64, max_mz: f64, peak_count_override: Option<usize>) -> ScanMessage {
        self.scan_number += 1;

        // Generate realistic peak count (500-2000 for MS1) unless overridden (stress tests).
        let peak_count = peak_count_override.unwrap_or_else(|| self.random.gen_range(500..2000));
        let (mz_values, intensity_values) = self.generate_spectrum(peak_count, min_mz, max_mz, 1e6, 1e8);

        // Calculate aggregates
        let (base_peak_mz, base_peak_intensity, tic) = calculate_aggregates(&mz_values, &intensity_values);

        let scan = ScanMessage {
            scan_number: self.scan_number,
            ms_order: 1,
            retention_time: self.retention_time,
            mz_values,
            intensity_values,
            base_peak_mz,
            base_peak_intensity,
            total_ion_current: tic,
            precursor_mass: None,
            precursor_charge: None,
            precursor_intensity: None,
            isolation_width: None,
            collision_energy: None,
            fragmentation_type: FragmentationType::FragmentationUnknown as i32,
            analyzer: "Orbitrap".to_string(),
            resolution_at_mz200: 120000.0,
            mass_accuracy_ppm: 3.0,
            polarity: Polarity::Positive as i32,
            timestamp_ms: current_timestamp_ms(),
            trailer_extra: Default::default(),
        };

        // Advance retention time (~0.5 seconds per cycle)
        self.retention_time += 0.5 / 60.0;

        scan
    }

    /// Generates an MS2 (fragmentation) scan based on a precursor
    pub fn generate_ms2(&mut self, precursor_mz: f64, precursor_intensity: f64, peak_count_override: Option<usize>) -> ScanMessage {
        self.scan_number += 1;

        // MS2 scans have fewer peaks (50-300) unless overridden (stress tests).
        let peak_count = peak_count_override.unwrap_or_else(|| self.random.gen_range(50..300));

        // Fragments are typically lower m/z than precursor
        let max_mz = precursor_mz * 0.95;
        let (mz_values, intensity_values) = self.generate_spectrum(
            peak_count,
            100.0,
            max_mz,
            precursor_intensity * 0.01,
            precursor_intensity * 0.5,
        );

        let (base_peak_mz, base_peak_intensity, tic) = calculate_aggregates(&mz_values, &intensity_values);

        let charge = self.random.gen_range(2..=4);

        ScanMessage {
            scan_number: self.scan_number,
            ms_order: 2,
            retention_time: self.retention_time,
            mz_values,
            intensity_values,
            base_peak_mz,
            base_peak_intensity,
            total_ion_current: tic,
            precursor_mass: Some(precursor_mz),
            precursor_charge: Some(charge),
            precursor_intensity: Some(precursor_intensity),
            isolation_width: Some(1.6),
            collision_energy: Some(30.0),
            fragmentation_type: FragmentationType::FragmentationHcd as i32,
            analyzer: "Orbitrap".to_string(),
            resolution_at_mz200: 30000.0, // Lower resolution for MS2
            mass_accuracy_ppm: 5.0,
            polarity: Polarity::Positive as i32,
            timestamp_ms: current_timestamp_ms(),
            trailer_extra: Default::default(),
        }
    }

    /// Generates a realistic-looking spectrum with isotopic patterns
    fn generate_spectrum(
        &mut self,
        peak_count: usize,
        min_mz: f64,
        max_mz: f64,
        min_intensity: f64,
        max_intensity: f64,
    ) -> (Vec<f64>, Vec<f64>) {
        let mut mz_values = Vec::with_capacity(peak_count);
        let mut intensity_values = Vec::with_capacity(peak_count);

        // Generate base peaks
        let base_peak_count = peak_count / 5; // ~20% are "real" peaks

        for _ in 0..base_peak_count {
            let base_mz = self.random.gen_range(min_mz..max_mz);
            let base_intensity = self.random.gen_range(min_intensity..max_intensity);

            // Add the monoisotopic peak
            mz_values.push(base_mz);
            intensity_values.push(base_intensity);

            // Add isotopic envelope (simplified model)
            // Real isotopic patterns depend on elemental composition
            let isotope_spacing = 1.003355; // ~1 Da for peptides

            // A+1 isotope (~60-80% of A)
            if self.random.gen_bool(0.8) {
                mz_values.push(base_mz + isotope_spacing);
                intensity_values.push(base_intensity * self.random.gen_range(0.4..0.8));
            }

            // A+2 isotope (~20-40% of A)
            if self.random.gen_bool(0.6) {
                mz_values.push(base_mz + 2.0 * isotope_spacing);
                intensity_values.push(base_intensity * self.random.gen_range(0.1..0.4));
            }
        }

        // Add noise peaks
        let noise_count = peak_count - mz_values.len();
        let noise_normal = Normal::new(0.0, min_intensity * 0.1).unwrap();

        for _ in 0..noise_count {
            let mz = self.random.gen_range(min_mz..max_mz);
            let noise: f64 = noise_normal.sample(&mut self.random).abs();
            let intensity = min_intensity * 0.01 + noise;

            mz_values.push(mz);
            intensity_values.push(intensity);
        }

        // Sort by m/z (required for spectrum data)
        let mut indices: Vec<usize> = (0..mz_values.len()).collect();
        indices.sort_by(|&a, &b| mz_values[a].partial_cmp(&mz_values[b]).unwrap());

        let sorted_mz: Vec<f64> = indices.iter().map(|&i| mz_values[i]).collect();
        let sorted_intensity: Vec<f64> = indices.iter().map(|&i| intensity_values[i]).collect();

        (sorted_mz, sorted_intensity)
    }

    /// Returns a random precursor from a simulated MS1 spectrum
    pub fn select_precursor(&mut self, ms1_scan: &ScanMessage) -> (f64, f64) {
        if ms1_scan.mz_values.is_empty() {
            return (500.0, 1e6); // Default fallback
        }

        // Select from top N most intense peaks
        let mut intensity_indices: Vec<(usize, f64)> = ms1_scan
            .intensity_values
            .iter()
            .enumerate()
            .map(|(i, &v)| (i, v))
            .collect();

        intensity_indices.sort_by(|a, b| b.1.partial_cmp(&a.1).unwrap());

        // Pick from top 20
        let top_n = intensity_indices.len().min(20);
        let selected_idx = self.random.gen_range(0..top_n);
        let (peak_idx, _) = intensity_indices[selected_idx];

        (
            ms1_scan.mz_values[peak_idx],
            ms1_scan.intensity_values[peak_idx],
        )
    }
}

fn calculate_aggregates(mz_values: &[f64], intensity_values: &[f64]) -> (f64, f64, f64) {
    if mz_values.is_empty() || intensity_values.is_empty() {
        return (0.0, 0.0, 0.0);
    }

    let mut max_idx = 0;
    let mut max_intensity = intensity_values[0];
    let mut tic = 0.0;

    for (i, &intensity) in intensity_values.iter().enumerate() {
        tic += intensity;
        if intensity > max_intensity {
            max_intensity = intensity;
            max_idx = i;
        }
    }

    (mz_values[max_idx], max_intensity, tic)
}

fn current_timestamp_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("Time went backwards")
        .as_millis() as i64
}

impl Default for ScanGenerator {
    fn default() -> Self {
        Self::new()
    }
}
