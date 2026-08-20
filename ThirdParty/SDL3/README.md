# Project-owned SDL3 native runtimes

The checked-in SDL3 shared libraries are generated from the SDL source commit
declared in `eng/sdl-toolchain.json`.

The first complete runtime set has not been imported yet. Run the manual
`SDL native runtimes` GitHub Actions workflow, download its
`sdl-native-bundle` artifact, and extract the contained `ThirdParty/SDL3`
directory at the repository root. The bundle contains every supported RID,
per-file provenance, SHA-256 values, SDL's license, and the MSBuild selection
file.

Before the first workflow run, generate and commit the local C# binding for the
manifest pin. The workflow preflight rejects an old or unprovenanced binding.

Do not add or replace an individual native library by hand. A complete bundle
must be generated and reviewed as one unit so every platform uses the same SDL
commit as the C# binding.
