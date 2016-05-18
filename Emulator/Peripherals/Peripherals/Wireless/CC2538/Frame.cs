﻿//
// Copyright (c) Antmicro
//
// This file is part of the Emul8 project.
// Full license details are defined in the 'LICENSE' file.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Emul8.Utilities;

namespace Emul8.Peripherals.Wireless.CC2538
{
    internal class Frame
    {
        public static Frame CreateACK(byte sequenceNumber, bool pending)
        {
            var result = new Frame();
            result.Length = 5;
            result.Type = FrameType.ACK;
            result.FramePending = pending;
            result.DataSequenceNumber = sequenceNumber;
            result.Encode();

            return result;
        }

        public Frame(byte[] data)
        {
            Bytes = data;
            Decode(data);
        }

        public byte Length { get; private set; }
        public FrameType Type { get; private set; }
        public bool SecurityEnabled { get; private set; }
        public bool FramePending { get; set; }
        public bool AcknowledgeRequest { get; private set; }
        public bool IntraPAN { get; private set; }
        public AddressingMode DestinationAddressingMode { get; private set; }
        public byte FrameVersion { get; private set; }
        public AddressingMode SourceAddressingMode { get; private set; }
        public byte DataSequenceNumber { get; private set; }
        public AddressInformation AddressInformation { get; private set; }
        public IList<byte> Payload { get; private set; }
        public byte[] Bytes { get; private set; }

        public string StringView
        {
            get
            {
                var result = new StringBuilder();
                var nonPrintableCharacterFound = false;
                for(int i = 0; i < Payload.Count; i++)
                {
                    if(Payload[i] < ' ' || Payload[i] > '~')
                    {
                        nonPrintableCharacterFound = true;
                    }
                    else {
                        if(nonPrintableCharacterFound)
                        {
                            result.Append('.');
                            nonPrintableCharacterFound = false;
                        }

                        result.Append((char)Payload[i]);
                    }
                }

                return result.ToString();
            }
        }

        private Frame()
        {
        }

        private int GetAddressingFieldsLength()
        {
            var result = 0;
            result += DestinationAddressingMode.GetBytesLength();
            result += SourceAddressingMode.GetBytesLength();
            if(DestinationAddressingMode != AddressingMode.None)
            {
                result += 2;
            }
            if(!IntraPAN && SourceAddressingMode != AddressingMode.None)
            {
                result += 2;
            }
            return result;
        }

        private void Decode(byte[] data)
        {
            Length = data[0];
            if(Length > 127)
            {
                throw new Exception("Frame length should not exceed 127 bytes.");
            }
            if(data.Length - 1 != Length)
            {
                throw new Exception("Frame length is inconsistent");
            }

            Type = (FrameType)(data[1] & 0x7);
            SecurityEnabled = (data[1] & 0x8) != 0;
            FramePending = (data[1] & 0x10) != 0;
            AcknowledgeRequest = (data[1] & 0x20) != 0;
            IntraPAN = (data[1] & 0x40) != 0;
            DestinationAddressingMode = (AddressingMode)((data[2] >> 2) & 0x3);
            FrameVersion = (byte)((data[2] >> 4) & 0x3);
            SourceAddressingMode = (AddressingMode)(data[2] >> 6);

            DataSequenceNumber = data[3];
            AddressInformation = new AddressInformation(DestinationAddressingMode, SourceAddressingMode, IntraPAN, new ArraySegment<byte>(data, 4, GetAddressingFieldsLength()));
            Payload = new ArraySegment<byte>(data, 3 + AddressInformation.Bytes.Count, Length - (5 + AddressInformation.Bytes.Count));
        }

        private void Encode()
        {
            var bytes = new List<byte>();

            bytes.Add(Length);
            var frameControlByte0 = (byte)Type;
            if(FramePending)
            {
                frameControlByte0 |= (0x1 << 4);
            }
            bytes.Add(frameControlByte0);
            bytes.Add(0); // frameControlByte1

            bytes.Add(DataSequenceNumber);
            if(AddressInformation != null && AddressInformation.Bytes.Count > 0)
            {
                bytes.AddRange(AddressInformation.Bytes);
            }
            if(Payload != null && Payload.Count > 0)
            {
                bytes.AddRange(Payload);
            }

            var crc = CalculateCRC(bytes.Skip(1));
            bytes.Add(crc[0]);
            bytes.Add(crc[1]);

            Bytes = bytes.ToArray();
        }

        public bool CheckCRC()
        {
            var crc = CalculateCRC(Bytes.Skip(1).Take(Bytes.Length - 3));
            return Bytes[Bytes.Length - 2] == crc[0] && Bytes[Bytes.Length - 1] == crc[1];
        }

        public static byte[] CalculateCRC(IEnumerable<byte> bytes)
        {
            var crc = 0x00;
            foreach(var b in bytes)
            {
                crc = AddByte(crc, BitHelper.ReverseBits(b));
            }

            return new[] { BitHelper.ReverseBits((byte)(crc >> 8)), BitHelper.ReverseBits((byte)crc) };
        }

        private static int AddByte(int currentCrc, byte b)
        {
            int newCrc = (((currentCrc >> 8) & 0xff) | (currentCrc << 8) & 0xffff);
            newCrc ^= b & 0xff;
            newCrc ^= (newCrc & 0xff) >> 4;
            newCrc ^= (newCrc << 12) & 0xffff;
            newCrc ^= (newCrc & 0xff) << 5;
            newCrc = newCrc & 0xffff;
            return newCrc;
        }
    }
}

