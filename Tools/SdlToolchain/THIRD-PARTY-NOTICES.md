# Third-party notices

The toolchain fetches pinned upstream source trees at build/generation time.
Those trees are not vendored into this repository.

## SDL

- Source: <https://github.com/libsdl-org/SDL>
- License: zlib
- Use: native runtime built and vendored by this toolchain; headers are the
  authoritative input for generated C# declarations.

The tracked `ThirdParty/SDL3` runtime tree includes SDL's `LICENSE.txt` from
the exact pinned source checkout.

## flibitijibibo/SDL3-CS

- Source: <https://github.com/flibitijibibo/SDL3-CS>
- License: zlib
- Use: build-time C# binding generator.

The generated output is altered by this project: its namespace and class name
are adapted, source provenance is inserted, the generator target framework is
adapted to .NET 10, and a known GPU flag definition is supplemented when that
flag exists in the selected SDL headers. It must not be represented as an
unmodified upstream source file.

SDL3-CS license notice:

```text
/* SDL3-CS - C# Bindings for SDL3
 *
 * Copyright (c) 2024 Colin Jackson
 *
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from
 * the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 * claim that you wrote the original software. If you use this software in a
 * product, an acknowledgment in the product documentation would be
 * appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not be
 * misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source distribution.
 *
 * Colin "cryy22" Jackson <c@cryy22.art>
 *
 */
```

## c2ffi

- Source: <https://github.com/rpav/c2ffi>
- License stated by upstream: GPL version 2 (see the pinned source notices)
- Use: build-time executable that converts the selected SDL headers to the
  JSON consumed by the binding generator.

`c2ffi` is not linked into the engine, game, generated binding, or SDL native
library.
