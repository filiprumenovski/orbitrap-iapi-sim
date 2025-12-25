fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Compile the protobuf file
    tonic_build::configure()
        .build_server(true)
        .build_client(false)
        .out_dir("src/proto")
        .compile_protos(
            &["../../../protos/simulator.proto"],
            &["../../../protos"],
        )?;

    // Rerun if proto file changes
    println!("cargo:rerun-if-changed=../../../protos/simulator.proto");

    Ok(())
}
