using System;
using System.Collections.Generic;

namespace AutomationTools.ViewModels
{
    /// <summary>
    /// Provides utility methods to format, parse, assemble, and disassemble Modbus data.
    /// Handles various data types like 16-bit, 32-bit words, floats, and bit fields.
    /// </summary>
    public static class ModbusDataFormatter
    {
        // Future Proofing: Defines how 32-bit values are assembled from two 16-bit registers.
        public enum WordOrder
        {
            BigEndian,    // ABCD (Standard Modbus)
            LittleEndian, // DCBA
            WordSwap,     // CDAB
            ByteSwap      // BADC
        }

        public enum NumericFormat
        {
            Decimal,
            Hexadecimal
        }

        /// <summary>
        /// Formats raw memory into a collection of Modbus registers based on the selected display mode and formatting options.
        /// </summary>
        public static IEnumerable<ModbusRegister> FormatRegisters(Span<short> rawMemory, int startAddress, ModbusDisplayMode mode, WordOrder wordOrder = WordOrder.BigEndian, NumericFormat numericFormat = NumericFormat.Decimal)
        {
            var registers = new List<ModbusRegister>();

            switch (mode)
            {
                case ModbusDisplayMode.Word16:
                    for (int i = 0; i < rawMemory.Length; i++)
                    {
                        registers.Add(new ModbusRegister
                        {
                            Address = startAddress + i,
                            Value = numericFormat == NumericFormat.Hexadecimal ? $"0x{(ushort)rawMemory[i]:X4}" : rawMemory[i].ToString()
                        });
                    }
                    break;

                case ModbusDisplayMode.Word32:
                case ModbusDisplayMode.Float32:
                    for (int i = 0; i < rawMemory.Length; i += 2)
                    {
                        if (i + 1 >= rawMemory.Length) break; // Orphaned register

                        byte[] bytes = Assemble32BitBytes(rawMemory[i], rawMemory[i + 1], wordOrder);

                        string valueStr = mode == ModbusDisplayMode.Float32 
                            ? BitConverter.ToSingle(bytes, 0).ToString("G") 
                            : (numericFormat == NumericFormat.Hexadecimal ? $"0x{BitConverter.ToInt32(bytes, 0):X8}" : BitConverter.ToInt32(bytes, 0).ToString());

                        registers.Add(new ModbusRegister
                        {
                            Address = startAddress + i,
                            Value = valueStr
                        });
                    }
                    break;

                case ModbusDisplayMode.Byte8:
                    for (int i = 0; i < rawMemory.Length; i++)
                    {
                        byte high = (byte)(rawMemory[i] >> 8);
                        byte low = (byte)(rawMemory[i] & 0xFF);

                        string valueStr = numericFormat == NumericFormat.Decimal
                            ? $"{high} | {low}"
                            : $"{high:X2} {low:X2}";

                        registers.Add(new ModbusRegister
                        {
                            Address = startAddress + i,
                            Value = valueStr
                        });
                    }
                    break;

                case ModbusDisplayMode.BitField:
                    for (int i = 0; i < rawMemory.Length; i++)
                    {
                        ushort uval = (ushort)rawMemory[i];
                        string binary = Convert.ToString(uval, 2).PadLeft(16, '0');
                        registers.Add(new ModbusRegister
                        {
                            Address = startAddress + i,
                            Value = $"{binary.Substring(0, 8)} {binary.Substring(8, 8)}"
                        });
                    }
                    break;
            }

            return registers;
        }

        /// <summary>
        /// Parses a user input string into a short array for writing to Modbus memory.
        /// </summary>
        public static bool ParseUserInput(string input, ModbusDisplayMode mode, WordOrder wordOrder, NumericFormat numericFormat, out short[] result)
        {
            result = Array.Empty<short>();

            try
            {
                switch (mode)
                {
                    case ModbusDisplayMode.Word16:
                        if (numericFormat == NumericFormat.Hexadecimal && input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            if (ushort.TryParse(input.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out ushort hval))
                            {
                                result = new short[] { (short)hval };
                                return true;
                            }
                            return false;
                        }
                        if (short.TryParse(input, out short val))
                        {
                            result = new short[] { val };
                            return true;
                        }
                        break;

                    case ModbusDisplayMode.Word32:
                    case ModbusDisplayMode.Float32:
                        byte[] bytes = new byte[4];
                        if (mode == ModbusDisplayMode.Float32)
                        {
                            if (!float.TryParse(input, out float f)) return false;
                            bytes = BitConverter.GetBytes(f);
                        }
                        else
                        {
                            if (numericFormat == NumericFormat.Hexadecimal && input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!uint.TryParse(input.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out uint uintVal)) return false;
                                bytes = BitConverter.GetBytes(uintVal);
                            }
                            else
                            {
                                if (!int.TryParse(input, out int intVal)) return false;
                                bytes = BitConverter.GetBytes(intVal);
                            }
                        }

                        result = Disassemble32BitBytes(bytes, wordOrder);
                        return true;

                    case ModbusDisplayMode.Byte8:
                        string cleanedBytes = input.Replace("|", " ").Trim();
                        string[] parts = cleanedBytes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            byte high, low;
                            if (numericFormat == NumericFormat.Decimal)
                            {
                                if (!byte.TryParse(parts[0], out high) || !byte.TryParse(parts[1], out low)) return false;
                            }
                            else
                            {
                                high = Convert.ToByte(parts[0], 16);
                                low = Convert.ToByte(parts[1], 16);
                            }
                            short byteVal = (short)((high << 8) | low);
                            result = new short[] { byteVal };
                            return true;
                        }
                        break;

                    case ModbusDisplayMode.BitField:
                        string cleanedBits = input.Replace(" ", "").Trim();
                        if (cleanedBits.Length <= 16)
                        {
                            short bitVal = Convert.ToInt16(cleanedBits, 2);
                            result = new short[] { bitVal };
                            return true;
                        }
                        break;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Assembles two 16-bit registers into a 4-byte array according to the specified word order.
        /// </summary>
        private static byte[] Assemble32BitBytes(short r1, short r2, WordOrder wordOrder)
        {
            // raw1 and raw2 are what we got from memory. On Windows, BitConverter is Little-Endian.
            // We want to return a 4-byte array that BitConverter.ToInt32 / ToSingle will read natively.
            
            byte[] b1 = BitConverter.GetBytes(r1); // [0] = low byte, [1] = high byte (Little Endian)
            byte[] b2 = BitConverter.GetBytes(r2);

            byte A = b1[1], B = b1[0], C = b2[1], D = b2[0]; // Assuming standard Modbus transmits High Byte first

            byte[] leBytes = new byte[4];
            
            // Standard Modbus 32-bit (Big Endian ABCD over network, but interpreted locally)
            // If the user expects ABCD:
            switch (wordOrder)
            {
                case WordOrder.BigEndian: // ABCD
                    leBytes[0] = D;
                    leBytes[1] = C;
                    leBytes[2] = B;
                    leBytes[3] = A;
                    break;
                case WordOrder.LittleEndian: // DCBA
                    leBytes[0] = A;
                    leBytes[1] = B;
                    leBytes[2] = C;
                    leBytes[3] = D;
                    break;
                case WordOrder.WordSwap: // CDAB
                    leBytes[0] = B;
                    leBytes[1] = A;
                    leBytes[2] = D;
                    leBytes[3] = C;
                    break;
                case WordOrder.ByteSwap: // BADC
                    leBytes[0] = C;
                    leBytes[1] = D;
                    leBytes[2] = A;
                    leBytes[3] = B;
                    break;
            }

            return leBytes;
        }

        /// <summary>
        /// Disassembles a 4-byte array into two 16-bit registers according to the specified word order.
        /// </summary>
        private static short[] Disassemble32BitBytes(byte[] leBytes, WordOrder wordOrder)
        {
            byte A=0, B=0, C=0, D=0;

            switch (wordOrder)
            {
                case WordOrder.BigEndian: // ABCD
                    D = leBytes[0];
                    C = leBytes[1];
                    B = leBytes[2];
                    A = leBytes[3];
                    break;
                case WordOrder.LittleEndian: // DCBA
                    A = leBytes[0];
                    B = leBytes[1];
                    C = leBytes[2];
                    D = leBytes[3];
                    break;
                case WordOrder.WordSwap: // CDAB
                    B = leBytes[0];
                    A = leBytes[1];
                    D = leBytes[2];
                    C = leBytes[3];
                    break;
                case WordOrder.ByteSwap: // BADC
                    C = leBytes[0];
                    D = leBytes[1];
                    A = leBytes[2];
                    B = leBytes[3];
                    break;
            }

            // Repack into 2 shorts (Little Endian local system)
            byte[] b1 = new byte[] { B, A };
            byte[] b2 = new byte[] { D, C };

            return new short[] { BitConverter.ToInt16(b1, 0), BitConverter.ToInt16(b2, 0) };
        }
    }
}
