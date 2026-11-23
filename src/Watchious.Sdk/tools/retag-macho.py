#!/usr/bin/env python3
# .NET has no watchOS runtime identifier, so NativeAOT output and its runtime libraries are built for iOS.
# The arm64 code is the same, but every object carries an LC_BUILD_VERSION platform tag and Apple's linker
# refuses to mix platforms. This rewrites that tag (iOS -> watchOS, iOS Simulator -> watchOS Simulator)
# in standalone objects and in every member of static archives, in place, without changing any sizes.
# Fat (universal) inputs are reduced to their arm64 slice first.
import argparse
import struct
import sys
from pathlib import Path

MH_MAGIC_64 = 0xFEEDFACF
LC_BUILD_VERSION = 0x32
MACH_HEADER_64_SIZE = 32
AR_MAGIC = b"!<arch>\n"
AR_HEADER_SIZE = 60
FAT_MAGIC = 0xCAFEBABE
FAT_ARCH_SIZE = 20
CPU_TYPE_ARM64 = 0x0100000C
CPU_SUBTYPE_ARM64_ALL = 0

PLATFORM_IOS = 2
PLATFORM_WATCHOS = 4
PLATFORM_IOSSIMULATOR = 7
PLATFORM_WATCHOSSIMULATOR = 9

PLATFORM_MAPS = {
    "watchos": {PLATFORM_IOS: PLATFORM_WATCHOS},
    "watchossimulator": {PLATFORM_IOSSIMULATOR: PLATFORM_WATCHOSSIMULATOR},
}


def encode_version(text):
    parts = (text.split(".") + ["0", "0"])[:3]
    major, minor, patch = (int(part) for part in parts)
    return (major << 16) | (minor << 8) | patch


def patch_macho(buffer, offset, platform_map, minos, sdk, name):
    if offset + MACH_HEADER_64_SIZE > len(buffer):
        return 0
    magic, _, _, _, ncmds, _, _, _ = struct.unpack_from("<IiiIIIII", buffer, offset)
    if magic != MH_MAGIC_64:
        return 0

    patched = 0
    position = offset + MACH_HEADER_64_SIZE
    for _ in range(ncmds):
        command, command_size = struct.unpack_from("<II", buffer, position)
        if command == LC_BUILD_VERSION:
            platform = struct.unpack_from("<I", buffer, position + 8)[0]
            if platform in platform_map:
                struct.pack_into("<III", buffer, position + 8, platform_map[platform], minos, sdk)
                patched += 1
            elif platform not in platform_map.values():
                raise SystemExit(f"{name}: unexpected platform {platform}")
        position += command_size
    return patched


def patch_archive(buffer, platform_map, minos, sdk, name):
    patched = 0
    position = len(AR_MAGIC)
    while position + AR_HEADER_SIZE <= len(buffer):
        header = buffer[position:position + AR_HEADER_SIZE]
        member_name = header[0:16].decode("ascii").strip()
        member_size = int(header[48:58].decode("ascii").strip())
        data_start = position + AR_HEADER_SIZE
        inline_name_length = int(member_name[3:]) if member_name.startswith("#1/") else 0
        patched += patch_macho(buffer, data_start + inline_name_length, platform_map, minos, sdk, f"{name}({member_name})")
        position = data_start + member_size
        position += position % 2
    return patched


# .NET for iOS ships some static libraries as fat (universal) files, even with a single arm64 slice.
def thin_to_arm64(buffer, name):
    if len(buffer) < 8 or struct.unpack_from(">I", buffer, 0)[0] != FAT_MAGIC:
        return buffer
    arch_count = struct.unpack_from(">I", buffer, 4)[0]
    for index in range(arch_count):
        cpu_type, cpu_subtype, offset, size, _ = struct.unpack_from(">iiIII", buffer, 8 + index * FAT_ARCH_SIZE)
        if cpu_type == CPU_TYPE_ARM64 and (cpu_subtype & 0xFF) == CPU_SUBTYPE_ARM64_ALL:
            return bytearray(buffer[offset:offset + size])
    raise SystemExit(f"{name}: no arm64 slice in the fat file")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", choices=sorted(PLATFORM_MAPS), required=True)
    parser.add_argument("--minos", required=True)
    parser.add_argument("--sdk", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("inputs", nargs="+")
    arguments = parser.parse_args()

    platform_map = PLATFORM_MAPS[arguments.target]
    minos = encode_version(arguments.minos)
    sdk = encode_version(arguments.sdk)
    output_dir = Path(arguments.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    for input_path in map(Path, arguments.inputs):
        buffer = thin_to_arm64(bytearray(input_path.read_bytes()), input_path.name)
        if buffer.startswith(AR_MAGIC):
            patched = patch_archive(buffer, platform_map, minos, sdk, input_path.name)
        else:
            patched = patch_macho(buffer, 0, platform_map, minos, sdk, input_path.name)
        (output_dir / input_path.name).write_bytes(buffer)
        print(f"{input_path.name}: {patched} object(s) re-tagged")


if __name__ == "__main__":
    sys.exit(main())
