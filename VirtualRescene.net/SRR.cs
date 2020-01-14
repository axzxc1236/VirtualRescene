using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace VirtualRescene.net
{
    public class RARmetadata
    {
        public byte[] header; //Header that's at the start of a RAR file.
        public byte[] file_end; //Data that's at the end of a RAR file.
    }
    class SRR
    {
        public static Dictionary<string, RARmetadata> Dump_headers(string srr_file)
        {
            if (!File.Exists(srr_file)) throw new FileNotFoundException();

            Process list_details = Process.Start(new ProcessStartInfo()
            {
                FileName = Program.SRR_exe,
                Arguments = "-e " + srr_file,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            Dictionary<string, RARmetadata> result = new Dictionary<string, RARmetadata>();
            RARmetadata metadata = new RARmetadata();
            string received_bytes = "";
            string filename = "";
            int mode = 0;
            list_details.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data == null) return;
                /* Mode 0: wait for "Block: SRR RAR subblock" text to show up
                 * Mode 1: wait for Rar name
                 * Mode 2: getting RAR headers, file size and modification time,
                 *         if hit the end of archive (Block: RAR Archive end) switch to mode 3
                 * Mode 3: Get bytes at the end of file, and then go back to mode 0.
                 */
                if (mode == 0 && e.Data.StartsWith("Block: SRR RAR subblock"))
                {
                    mode = 1;
                    metadata = new RARmetadata();
                }
                else if (mode == 1 && e.Data.StartsWith("+Rar name: "))
                {
                    filename = e.Data.Replace("+Rar name: ", "");
                    mode = 2;
                }
                else if (mode == 2 && e.Data.StartsWith("|Header bytes: "))
                    received_bytes = string.Concat(
                        received_bytes,
                        e.Data.Replace("|Header bytes: ", "")
                    );
                else if (mode == 2 && e.Data.StartsWith("Block: RAR Archive end"))
                {
                    //result.Add(filename, Hex_to_Bytes(received_bytes));
                    metadata.header = Hex_to_Bytes(received_bytes);
                    received_bytes = "";
                    mode = 3;
                }
                else if (mode == 3 && e.Data.StartsWith("|Header bytes: "))
                {
                    //result.Add(filename, Hex_to_Bytes(received_bytes));
                    metadata.file_end = Hex_to_Bytes(e.Data.Replace("|Header bytes: ", ""));
                    mode = 0;
                    result.Add(filename, metadata);
                }
            };
            list_details.BeginOutputReadLine();
            list_details.WaitForExit();
            return result;
        }

        public static Dictionary<string, long> Dump_RAR_sizes(string srr_file)
        {
            if (!File.Exists(srr_file)) throw new FileNotFoundException();

            Process list_details = Process.Start(new ProcessStartInfo()
            {
                FileName = Program.SRR_exe,
                Arguments = "-l " + srr_file,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            Dictionary<string, long> result = new Dictionary<string, long>();
            int mode = 0;
            list_details.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data == null) return;
                Console.WriteLine(e.Data);
                if (mode == 0 && e.Data == "RAR files:")
                    mode = 1;
                else if (mode == 1)
                {
                    if (e.Data.StartsWith("Archived files:")) mode = 2;
                    string[] tmp = e.Data.Trim().Split(' ');
                    if (tmp.Length != 3)
                    {
                        list_details.Close();
                        return;
                    }
                    result.Add(tmp[0], Convert.ToUInt32(tmp[2], 10));
                }
            };
            list_details.BeginOutputReadLine();
            list_details.WaitForExit();
            return result;
        }

        private static byte[] Hex_to_Bytes(string input)
        {
            byte[] result = new byte[input.Length / 2];
            for (int i = 0; i < input.Length; i += 2)
                result[i / 2] = Convert.ToByte(input.Substring(i, 2), 16);
            return result;
        }
    }
}
