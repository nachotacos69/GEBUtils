﻿using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.IO;

namespace GEBUtils.FHandlers
{
    class TR2Info
    {
        public static byte[] header = new byte[6] { 0x2E, 0x74, 0x72, 0x32, 0x02, 0x00 };
        public static byte[] endingheader = new byte[5] { 0x00, 0x74, 0x72, 0x32, 0x00 };
        public static byte[] specialendingheader = new byte[9] { 0x00, 0x74, 0x72, 0x32, 0x00, 0x65, 0x6E, 0x75, 0x73 };

        // Header Values
        public static byte[] nameSeparator = new byte[4] { 0x40, 0x00, 0x00, 0x00 };
        public static int nameLenght = 0x30;
        public static byte[] columnIdentifier = new byte[4] { 0x14, 0x00, 0x00, 0x00 };

        // Column Values
        public static byte[] columnNameSeparator = new byte[4] { 0x00, 0x02, 0xDF, 0x07 };
        public static int columnNameLenght = 0x3C;
        public static int columnDataTypeLenght = 0x30;

        // Special Columns Values
        public static byte[] INT16DataType = new byte[0x0C] { 0x00, 0x00, 0x00, 0x00, 0x73, 0x04, 0x01, 0x00, 0x7F, 0x00, 0x0C, 0x00 };
        public static byte[] INT32DataType = new byte[0x0C] { 0x00, 0x00, 0x00, 0x00, 0x6C, 0x04, 0x01, 0x00, 0x7F, 0x00, 0x0C, 0x00 };
        public static byte[] UTF8DataType = new byte[0x0C] { 0x00, 0x00, 0x00, 0x00, 0x55, 0x02, 0x01, 0x00, 0xFF, 0x00, 0x0C, 0x00 };
        public static byte[] ASCIIDataType = new byte[0x0C] { 0x00, 0x00, 0x00, 0x00, 0x55, 0x02, 0x01, 0x00, 0x7F, 0x00, 0x0C, 0x00 };

        #region Structures

        public struct Column
        {
            public uint id;
            public uint offset;
            public uint lenght;

            public string name;
            public string dataType;

            public Object[] data;
        };

        #endregion
    }

    class TR2
    {
        public int tableType;
        public string fileName;
        public int columnNum;
        public TR2Info.Column[] columnsList;
        public int rowNum;
        public List<byte[]> rowHeaderValues;
        public byte[] endingPointers;

        public TR2(string tr2path)
        {
            FileStream fs = new FileStream(tr2path, FileMode.Open, FileAccess.Read);

            // Reading TR2 type
            fs.Seek(0x06, SeekOrigin.Begin);
            byte[] buffer = new byte[2];
            fs.Read(buffer, 0, 2);
            tableType = TR2Utils.ConvertNumber(buffer);

            // Reading real TR2 name
            buffer = new byte[0x30];
            fs.Read(buffer, 0, 0x30);
            fileName = Encoding.UTF8.GetString(buffer).Replace("\0", "");

            // Reading number of tables
            fs.Seek(0x3C, SeekOrigin.Begin);
            buffer = new byte[4];
            fs.Read(buffer, 0, 4);
            columnNum = TR2Utils.ConvertNumber(buffer);

            // Columns Information Reader Cicle
            //columnsList = new List<TR2Info.Column>();
            columnsList = new TR2Info.Column[columnNum];

            for (int i = 0; i < columnNum; i++)
            {
                buffer = new byte[0x14];
                fs.Read(buffer, 0, 0x14);
                byte[] tempBuffer = new byte[4];

                Buffer.BlockCopy(buffer, 0, tempBuffer, 0, 0x04); // Getting the column id
                columnsList[i].id = (uint)TR2Utils.ConvertNumber(tempBuffer);
                Buffer.BlockCopy(buffer, 0x04, tempBuffer, 0, 0x04); // Getting the column offset
                columnsList[i].offset = (uint)TR2Utils.ConvertNumber(tempBuffer);
                Buffer.BlockCopy(buffer, 0x0C, tempBuffer, 0, 0x04); // Getting the column dimension
                columnsList[i].lenght = (uint)TR2Utils.ConvertNumber(tempBuffer);
            }

            // Reading rows number
            buffer = new byte[4];
            fs.Read(buffer, 0, 4);
            rowNum = TR2Utils.ConvertNumber(buffer);

            // Reading Row Headers Values
            rowHeaderValues = new List<byte[]>();
            for (int i = 0; i < rowNum; i++)
            {
                buffer = new byte[4];
                fs.Read(buffer, 0, 4);
                rowHeaderValues.Add(buffer);
            }

            // Column Reader Cicle
            for (int i = 0; i < columnNum; i++)
            {
                fs.Seek(columnsList[i].offset, SeekOrigin.Begin);
                byte[] colBuffer = new byte[columnsList[i].lenght];
                fs.Read(colBuffer, 0, (int)columnsList[i].lenght);

                // Reading Column Name
                byte[] tempBuffer = new byte[0x3C];
                Buffer.BlockCopy(colBuffer, 0, tempBuffer, 0, 0x3C);
                columnsList[i].name = Encoding.UTF8.GetString(tempBuffer).Replace("\0", "");

                // Reading Column Data Type
                tempBuffer = new byte[0x30];
                Buffer.BlockCopy(colBuffer, 0x40, tempBuffer, 0, 0x30);
                columnsList[i].dataType = Encoding.UTF8.GetString(tempBuffer).Replace("\0", "");

                // Table data reader cycle
                columnsList[i].data = new Object[rowNum];

                for (int x = 0; x < rowNum; x++)
                {
                    byte[] limitBuffer = new byte[4];
                    Buffer.BlockCopy(colBuffer, (0x80 + (x * 0x04)), limitBuffer, 0, 0x04);
                    int lowLimit = TR2Utils.ConvertNumber(limitBuffer);
                    Buffer.BlockCopy(colBuffer, (0x84 + (x * 0x04)), limitBuffer, 0, 0x04);
                    int highLimit = TR2Utils.ConvertNumber(limitBuffer);

                    byte[] dataBuffer = new byte[highLimit - lowLimit];
                    Buffer.BlockCopy(colBuffer, lowLimit, dataBuffer, 0, (highLimit - lowLimit));

                    // Numerical Data
                    if ((columnsList[i].dataType == "INT16") || (columnsList[i].dataType == "INT32"))
                    {
                        columnsList[i].data[x] = (Object)TR2Utils.ConvertNumber(dataBuffer);
                    }
                    // Text Data
                    if ((columnsList[i].dataType == "ASCII") || (columnsList[i].dataType == "UTF-8"))
                    {
                        columnsList[i].data[x] = (Object)TR2Utils.ConvertString(dataBuffer, columnsList[i].dataType);
                    }
                }
            }

            // Reading Ending Pointer
            endingPointers = new byte[0x0C];
            fs.Seek(columnsList[columnNum - 1].offset + columnsList[columnNum - 1].lenght, SeekOrigin.Begin);
            fs.Read(endingPointers, 0, 0x0C);
        }
    }

    class TR2Utils
    {
        public static int ConvertNumber(byte[] rawdata)
        {
            int num = 0;

            for (int i = 0; i < rawdata.Length; i++)
            {
                num += rawdata[i] * (int)(Math.Pow(256, i));
            }

            return num;
        }

        public static string ConvertString(byte[] rawdata, string type)
        {
            if (type == "ASCII")
                return Encoding.ASCII.GetString(rawdata).Replace("\0", "");
            return Encoding.UTF8.GetString(rawdata).Replace("\0", "");
        }
    }
}
