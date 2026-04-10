using RagnarokReplay;
using System;
using System.IO;
using System.Text;

namespace RagnarokReplayExample
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: RagnarokReplayExample <input.rrf> [outputPath]");
                return;
            }

            string inputPath = args[0];

            // 取輸入資料夾
            string inputDir = Path.GetDirectoryName(inputPath);

            // 取不含副檔名的檔名
            string baseName = Path.GetFileNameWithoutExtension(inputPath);

            string outputPath;

            // ★ 新增：如果有指定輸出路徑
            if (args.Length >= 2)
            {
                string userPath = args[1];

                // 如果是資料夾
                if (Directory.Exists(userPath) ||
                    userPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    Directory.CreateDirectory(userPath);
                    outputPath = Path.Combine(userPath, baseName + ".txt");
                }
                else
                {
                    // 視為完整檔名
                    string outDir = Path.GetDirectoryName(userPath);
                    if (!string.IsNullOrEmpty(outDir))
                        Directory.CreateDirectory(outDir);

                    outputPath = userPath;
                }
            }
            else
            {
                // 原本行為：與 RRF 同資料夾
                outputPath = Path.Combine(inputDir, baseName + ".txt");
            }

            // ★ StringBuilder：高速暫存輸出
            var sb = new StringBuilder(100_000_000);

            var replay = new Replay();
            replay.LoadFile(inputPath);

            foreach (var chunk in replay.ChunkContainers)
            {
                sb.AppendLine($"ContainerType {chunk.ContainerType}");

                switch (chunk.ContainerType)
                {
                    case ContainerType.PacketStream:
                        foreach (var packet in chunk.Data)
                        {
                            if (!Enum.IsDefined(typeof(HEADER), packet.Header))
                            {
                                sb.AppendLine($"[+{ConvertMsToTime(packet.Time)}] Unknown packet {packet.Header}");
                            }
                            else
                            {
                                sb.AppendLine($"[+{ConvertMsToTime(packet.Time)}] packet {(HEADER)packet.Header}");
                                sb.AppendLine(packet.Data.Hexdump());
                            }
                        }
                        break;

                    case ContainerType.InitialPackets:
                        foreach (var packet in chunk.Data)
                        {
                            if (!Enum.IsDefined(typeof(ReplayOpCodes), (short)packet.Id))
                            {
                                sb.AppendLine($"Unknown initial packet: {packet.Id}");
                            }
                            else
                            {
                                sb.AppendLine($"Unparsed initial packet: {(ReplayOpCodes)packet.Id}");
                            }
                        }
                        break;

                    case ContainerType.ReplayData:
                    case ContainerType.Session:
                    case ContainerType.Status:
                    case ContainerType.Quests:
                    case ContainerType.GroupAndFriends:
                    case ContainerType.Items:
                    case ContainerType.UnknownContainingPet:
                    case ContainerType.Efst:
                        foreach (var entry in chunk.Data)
                        {
                            if (!Enum.IsDefined(typeof(ReplayOpCodes), (short)entry.Id))
                            {
                                sb.AppendLine($"Unknown opcode {entry.Id}");
                                sb.AppendLine(entry.Data.Hexdump());
                            }
                            else
                            {
                                sb.AppendLine($"[Chunk {chunk.ContainerType}] Unparsed opcode {(ReplayOpCodes)entry.Id}, Length={entry.Length}");

                                switch ((ReplayOpCodes)entry.Id)
                                {
                                    case ReplayOpCodes.Charactername:
                                    case ReplayOpCodes.Mapname:
                                        string str = Encoding.UTF8.GetString(entry.Data).TrimEnd('\0');
                                        sb.AppendLine($"    → Text: {str}");
                                        sb.AppendLine("    → Raw hex:");
                                        sb.AppendLine(entry.Data.Hexdump());
                                        break;

                                    case ReplayOpCodes.PosX:
                                    case ReplayOpCodes.PosY:
                                    case ReplayOpCodes.Direction:
                                    case ReplayOpCodes.Sex:
                                    case ReplayOpCodes.Region:
                                    case ReplayOpCodes.Service:
                                    case ReplayOpCodes.Maptype:
                                    case ReplayOpCodes.Length:
                                        if (entry.Data.Length >= 4)
                                        {
                                            int value = BitConverter.ToInt32(entry.Data, 0);
                                            sb.AppendLine($"    → Value: {value}");
                                        }
                                        else
                                        {
                                            sb.AppendLine("    → Data too short to convert to int");
                                        }
                                        break;

                                    default:
                                        sb.AppendLine("    → Raw hex:");
                                        sb.AppendLine(entry.Data.Hexdump());
                                        break;
                                }
                            }
                        }
                        sb.AppendLine();
                        break;

                    default:
                        sb.AppendLine($"Unhandled container type {chunk.ContainerType}");
                        foreach (var entry in chunk.Data)
                        {
                            if (!Enum.IsDefined(typeof(ReplayOpCodes), (short)entry.Id))
                            {
                                sb.AppendLine($"Opcode ID={entry.Id} (0x{entry.Id:X4}), Length={entry.Length}");
                                sb.AppendLine($"Unknown opcode {entry.Id}");
                                sb.AppendLine(entry.Data.Hexdump());
                            }
                            else
                            {
                                sb.AppendLine($"Opcode ID={entry.Id} (0x{entry.Id:X4}), Length={entry.Length}");
                                sb.AppendLine($"[Chunk {chunk.ContainerType}] Unparsed opcode {(ReplayOpCodes)entry.Id}, Length={entry.Length}");
                                sb.AppendLine(entry.Data.Hexdump());
                            }
                        }
                        sb.AppendLine();
                        break;
                }
            }

            // ★ 一次性寫出
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static string ConvertMsToTime(int ms)
        {
            var uSec = ms % 1000;
            ms /= 1000;

            var seconds = ms % 60;
            ms /= 60;

            var minutes = ms % 60;
            ms /= 60;

            var hours = ms % 60;

            return $"{hours:00}:{minutes:00}:{seconds:00}:{uSec:000}";
        }
    }
}
