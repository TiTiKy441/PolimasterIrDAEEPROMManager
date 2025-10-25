# PolimasterIrDAEEPROMManager
Utility for reading/writing/verifying EEPROM contents of the Polimaster's radiation pagers via an infrared port

## Download executable
If you are here just to download the win10x64 exe: [Click here](https://github.com/TiTiKy441/PolimasterIrDAEEPROMManager/releases/download/latest/win-x64-self-contained.zip)

## How to use
```
Description:
  Utility for reading/writing/verifying EEPROM contents of the Polimaster's radiation pagers

Usage:
  PolimasterIrDAEEPROMManager [options]

Parameters:
  -s, --start <start> (REQUIRED)                  Operation's start address [default: 0]
  -e, --end <end> (REQUIRED)                      Operation's end address [default: 1024]
  -o, --operation <Read|Verify|Write> (REQUIRED)  Type of operation to execute, r for read, w for write, v for verify [default: Read]
  -f, --file <file> (REQUIRED)                    Output/input file [default: eeprom_dump.hex]
  --erase                                         Erases contents of the file before writing EEPROM data to it
  --reverse-addresses                             Read memory in reverse
  --debug-irda                                    Print irda debug information
  -?, -h, --help                                  Show help and usage information
  --version                                       Show version information
```

## Notes
Usual EEPROM size is 8192 bytes, but protocol allows to go up to 16384.

Use `--reverse-addresses` for models that are not PM1401 or PM1703

If long reads randomly stall because of a connection reset, try putting in a fresh battery, works great on PM1603 watches

PM1208 watches are not supported and never will be

## IrDA debug statuses
`!` at the end means that the operation has finished executing

`P` - wait until port is free

`C` - wait until device connected

`F` - flushing already existing data from the stream

`S` - sedning data

`W` - wait for the device's response

`R` - read data 

If device have not responded (status `W`) withing 500 ms, will attempt to send data again and wait for the response, will attempt to resend it two times and then raise a timeout exception

## Premade commands
`PolimasterIrDAEEPROMManager -s 0 -e 8192` - to dump PM1401 or PM1703 (?) memory

`PolimasterIrDAEEPROMManager -s 0 -e 8192 --reverse-addresses` - to dump PM1603,PM1621,... memory
