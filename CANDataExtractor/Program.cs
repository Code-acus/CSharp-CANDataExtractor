

class Program
{
    static void Main()
    {
        // Open the input file
        using var input_file = new FileStream(@"C:\Temp_Data\mg1cs002-stockmapsflash.candata", FileMode.Open, FileAccess.Read);

        // Open the output file
        using var output_file = new FileStream(@"C:\Temp_Data\mg1cs002-stockmapsflash.transferdata.bin", FileMode.Create, FileAccess.Write);

        // Define the relevant CAN IDs and UDS service numbers
        const ushort engine_id = 0x7E8;
        const ushort dongle_id = 0x7E0;
        const byte write_data_service = 0x36;

        // Read the input file and extract the relevant data
        int message_count = 0;
        int relevant_count = 0;
        var transfer_data = new List<uint>();
        bool transfer_started = false;
        ushort transfer_data_length = 0;
        while (true)
        {
            try
            {
                // Read the timestamp
                ulong timestamp = BitConverter.ToUInt64(ReadBytes(input_file, sizeof(ulong)));

                // Read the CAN ID
                ushort can_id = BitConverter.ToUInt16(ReadBytes(input_file, sizeof(ushort)));

                // Read the CAN data
                byte[] can_data = ReadBytes(input_file, sizeof(byte) * 8);

                // Count the messages
                message_count++;

                // Check if the message is relevant
                if ((can_id == engine_id && can_data[0] == write_data_service) || // Engine to dongle
                    (can_id == dongle_id && can_data[1] == write_data_service))   // Dongle to engine
                {
                    relevant_count++;

                    // Extract the transfer data
                    if (transfer_started)
                    {
                        ushort remaining_length = (ushort)(transfer_data_length - transfer_data.Count);
                        for (int i = 0; i < remaining_length && i < 4; i++)
                        {
                            int byte_index = i + 4;
                            int bit_index = 7 - (transfer_data.Count % 8);
                            bool bit_value = ((can_data[byte_index] >> bit_index) & 1) == 1; // fix here
                            if (bit_value)
                            {
                                transfer_data.Add(1u);
                            }
                            else
                            {
                                transfer_data.Add(0u);
                            }
                        }
                        if (transfer_data.Count == transfer_data_length)
                        {
                            // Write the transfer data to the output file
                            uint transfer_data_uint = 0;
                            for (int i = 0; i < transfer_data.Count; i++)
                            {
                                transfer_data_uint |= transfer_data[i] << (i % 32);
                            }
                            output_file.Write(BitConverter.GetBytes(transfer_data_uint), 0, transfer_data_length / 8);

                            // Reset the transfer state
                            transfer_started = false;
                            transfer_data.Clear();
                            transfer_data_length = 0;
                        }
                    }
                    else
                    {
                        transfer_started = true;
                        transfer_data_length = (ushort)(can_data[2] * 8);
                    }
                }
            }
            catch (EndOfStreamException)
            {
                break;
            }
        }

        // Close the files
        input_file.Close();
        output_file.Close();

        // Print the results
        Console.WriteLine($"Total messages: {message_count}");
        Console.WriteLine($"Relevant messages: {relevant_count}");
    }

    static byte[] ReadBytes(FileStream stream, int count)
    {
        var bytes = new byte[count];
        int read = stream.Read(bytes, 0, count);
        if (read != count)
        {
            throw new EndOfStreamException();
        }
        return bytes;
    }
}
