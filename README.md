# PolimasterIrDAEEPROMManager
Utility for reading/writing/verifying EEPROM contents of the Polimaster's PM1703 and PM1401 series radiation pagers via an infrared port

## How to use
```
Description:
  Utility for reading/writing/verifying EEPROM contents of the Polimaster's PM1703 and PM1401 series radiation pagers

Usage:
  PolimasterIrDAEEPROMManager [options]

Options:
  -s, --start <start> (REQUIRED)          Operation's start address [default: 0]
  -e, --end <end> (REQUIRED)              Operation's end address [default: 1024]
  -o, --operation <operation> (REQUIRED)  Type of operation to execute, r for read, w for write, v for verify [default: r]
  -f, --file <file> (REQUIRED)            Output/input file [default: eeprom_dump.hex]
  -?, -h, --help                          Show help and usage information
  --version                               Show version information
```
