using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace PkgEdit.Model
{
    public abstract class PkgBase : INotifyPropertyChanged
    {
        public enum Id : uint
        {
            Header = 1,
            Kernel1 = 2,
            Ramdisk = 3,
            Installer = 4,
            InstallerXml = 5,
            InstallerSh = 15,
            Kernel2 = 14,
            Directory = 16,
            File = 17,
            FileSystem = 19,
        }

        public enum Permission : ushort
        {
            rwx = 7,
            rw = 6,
            rx = 5,
            r = 4,
            wx = 3,
            w = 2,
            x = 1,
        }

        public static byte Read1(Stream s)
        {
            int fbyte = s.ReadByte();

            if (fbyte < 0)
                throw new EndOfStreamException("Error reading byte");

            return (byte)fbyte;
        }

        public static void Write1(Stream s, byte d)
        {
            s.WriteByte(d);
        }

        public static ushort Read2(Stream s)
        {
            ushort result = 0;
            byte[] tmp = new byte[2];
            s.Read(tmp, 0, 2);
            for (int i = 0; i < 2; i++)
                result |= (ushort)(tmp[i] << (i * 8));
            return result;
        }

        public static void Write2(Stream s, ushort d)
        {
            byte[] t = new byte[2];
            t[0] = (byte)(d & 0xFF);
            t[1] = (byte)((d >> 8) & 0xFF);
            s.Write(t, 0, 2);
        }

        public static uint Read4(Stream s)
        {
            uint result = 0;
            byte[] tmp = new byte[4];
            s.Read(tmp, 0, 4);
            for (int i = 0; i < 4; i++)
                result |= (uint)(tmp[i] << (i * 8));
            return result;
        }

        public static void Write4(Stream s, uint d)
        {
            byte[] t = new byte[4];
            t[0] = (byte)(d & 0xFF);
            t[1] = (byte)((d >> 8) & 0xFF);
            t[2] = (byte)((d >> 16) & 0xFF);
            t[3] = (byte)((d >> 24) & 0xFF);
            s.Write(t, 0, 4);
        }

        public static string ReadString(Stream s)
        {
            StringBuilder str = new StringBuilder();

            do
            {
                int fbyte = s.ReadByte();

                if (fbyte < 0)
                    throw new EndOfStreamException("Error reading byte");

                char fchar = (char)fbyte;

                if (fchar != 0)
                    str.Append(fchar);
                else
                    break;
            } while (true);

            return str.ToString();
        }

        public static void WriteString(Stream s, string str)
        {
            s.Write(System.Text.Encoding.ASCII.GetBytes(str), 0, str.Length);
            s.WriteByte(0);
        }

        public static void WriteStringWithout0(Stream s, string str)
        {
            s.Write(System.Text.Encoding.ASCII.GetBytes(str), 0, str.Length);
        }

        public static byte[] ReadArray(Stream s, int size)
        {
            byte[] result = new byte[size];
            s.Read(result, 0, size);
            return result;
        }

        public static void WriteArray(Stream s, byte[] d)
        {
            s.Write(d, 0, d.Length);
        }

        public static uint Swap4(uint d)
        {
            return (d & 0x000000FFU) << 24
                 | (d & 0x0000FF00U) << 8
                 | (d & 0x00FF0000U) >> 8
                 | (d & 0xFF000000U) >> 24;
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder();
            foreach (byte b in ba)
                hex.AppendFormat("{0:X2}", b);
            return hex.ToString();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class Pkg : PkgBase
    {
        public ObservableCollection<PkgChunk> Chunks { get; set; }

        public Pkg()
        {
            Chunks = new ObservableCollection<PkgChunk>();
        }

        public void LoadPackageFromFile(string path)
        {
            FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read);
            file.Seek(16, SeekOrigin.Current);

            while (file.Position < file.Length)
            {
                Id id = (Id)Read4(file);
                uint size = Read4(file);
                long offset = file.Position - 8;
                bool bad = false;

                switch (id)
                {
                    case Id.Header:
                        Chunks.Add(new PkgHeader());
                        break;
                    case Id.Kernel1:
                        Chunks.Add(new PkgKernel1());
                        break;
                    case Id.Ramdisk:
                        Chunks.Add(new PkgRamdisk());
                        break;
                    case Id.Installer:
                        Chunks.Add(new PkgInstaller());
                        break;
                    case Id.InstallerXml:
                        Chunks.Add(new PkgInstallerXml());
                        break;
                    case Id.InstallerSh:
                        Chunks.Add(new PkgInstallerSh());
                        break;
                    case Id.Kernel2:
                        Chunks.Add(new PkgKernel2());
                        break;
                    case Id.Directory:
                        Chunks.Add(new PkgDirectory());
                        break;
                    case Id.File:
                        Chunks.Add(new PkgFile());
                        break;
                    case Id.FileSystem:
                        Chunks.Add(new PkgFileSystem());
                        break;
                    default:
                        bad = true;
                        break;
                }

                if (!bad)
                    Chunks.Last().LoadChunkFromStream(file, (int)size);

                long skip = offset + 8 + size;
                if ((skip % 4) != 0)
                    skip += 4 - (skip % 4);
                file.Seek(skip, SeekOrigin.Begin);
            }
        }

        public void SavePackageToFile(string path)
        {
            FileStream file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            file.Seek(16, SeekOrigin.Begin);

            foreach (PkgChunk chunk in Chunks)
            {
                chunk.SaveChunkToStream(file);
                file.Seek(0, SeekOrigin.End);
                if ((file.Position % 4) != 0)
                    file.Seek(4 - (file.Position % 4), SeekOrigin.Current);
                file.Flush();
            }

            file.Seek(0, SeekOrigin.End);

            if ((file.Position % 4) != 0)
                WriteArray(file, new byte[4 - (file.Position % 4)]);

            file.Seek(16, SeekOrigin.Begin);
            byte[] hash = MD5.Create().ComputeHash(file);
            file.Seek(0, SeekOrigin.Begin);
            file.Write(hash, 0, hash.Length);

            file.Close();
        }
    }

    public abstract class PkgChunk : PkgBase
    {
        public abstract string AsString { get; }
        public abstract string GetPreferredFileName();
        public abstract void LoadChunkFromStream(Stream s, int size);
        public abstract void SaveChunkToStream(Stream s);
        public abstract void ExportDataToStream(Stream s);
    }

    public class PkgHeader : PkgChunk
    {
        private byte[] unknown1;
        private string systemType1;
        private string systemType2;
        private string buildSystem;
        private string date;
        private string time;
        private string packageType1;
        private string packageType2;

        public byte[] Unknown1
        {
            get { return unknown1; }
            set { SetField(ref unknown1, value); }
        }
        public string SystemType1
        {
            get { return systemType1; }
            set { SetField(ref systemType1, value); }
        }
        public string SystemType2
        {
            get { return systemType2; }
            set { SetField(ref systemType2, value); }
        }
        public string BuildSystem
        {
            get { return buildSystem; }
            set { SetField(ref buildSystem, value); }
        }
        public string Date
        {
            get { return date; }
            set { SetField(ref date, value); }
        }
        public string Time
        {
            get { return time; }
            set { SetField(ref time, value); }
        }
        public string PackageType1
        {
            get { return packageType1; }
            set { SetField(ref packageType1, value); }
        }
        public string PackageType2
        {
            get { return packageType2; }
            set { SetField(ref packageType2, value); OnPropertyChanged("AsString"); }
        }

        public override string AsString { get { return "Header - " + PackageType2; } }

        public override string GetPreferredFileName()
        {
            return "Header.txt";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            Unknown1 = ReadArray(s, 12);
            SystemType1 = ReadString(s);
            SystemType2 = ReadString(s);
            BuildSystem = ReadString(s);
            Date = ReadString(s);
            Time = ReadString(s);
            PackageType1 = ReadString(s);
            PackageType2 = ReadString(s);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.Header);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            WriteArray(s, Unknown1);
            WriteString(s, SystemType1);
            WriteString(s, SystemType2);
            WriteString(s, BuildSystem);
            WriteString(s, Date);
            WriteString(s, Time);
            WriteString(s, PackageType1);
            WriteString(s, PackageType2);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteStringWithout0(s, "0x" + ByteArrayToString(Unknown1) + "\r\n");
            WriteStringWithout0(s, SystemType1 + "\r\n");
            WriteStringWithout0(s, SystemType2 + "\r\n");
            WriteStringWithout0(s, BuildSystem + "\r\n");
            WriteStringWithout0(s, Date + "\r\n");
            WriteStringWithout0(s, Time + "\r\n");
            WriteStringWithout0(s, PackageType1 + "\r\n");
            WriteStringWithout0(s, PackageType2 + "\r\n");
        }
    }

    public class PkgKernel1 : PkgChunk
    {
        private byte[] data;

        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public override string AsString { get { return "Kernel 1"; } }

        public override string GetPreferredFileName()
        {
            return "uImage1";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(16, SeekOrigin.Current);
            int dataSize = (int)size - 16;
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.Kernel1);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgRamdisk : PkgChunk
    {
        private byte[] data;

        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public override string AsString { get { return "Ramdisk"; } }

        public override string GetPreferredFileName()
        {
            return "ramdisk.gz";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(16, SeekOrigin.Current);
            int dataSize = (int)size - 16;
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.Ramdisk);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgInstaller : PkgChunk
    {
        private byte[] data;

        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public override string AsString { get { return "Installer"; } }

        public override string GetPreferredFileName()
        {
            return "lfo-pkg-install.elf";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(16, SeekOrigin.Current);
            int dataSize = (int)size - 16;
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.Installer);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgInstallerXml : PkgChunk
    {
        private byte[] data;

        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public override string AsString { get { return "Installer Xml"; } }

        public override string GetPreferredFileName()
        {
            return "lfo-pkg-install.xml";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(16, SeekOrigin.Current);
            int dataSize = (int)size - 16;
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.InstallerXml);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgInstallerSh : PkgChunk
    {
        private ushort order;
        private string name;
        private byte[] data;

        public ushort Order
        {
            get { return order; }
            set { SetField(ref order, value); }
        }
        public string Name
        {
            get { return name; }
            set { SetField(ref name, value); OnPropertyChanged("AsString"); }
        }
        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public PkgInstallerSh()
        {
            order = 0xFFFF;
        }

        public override string AsString { get { return "Installer Sh - " + Name; } }

        public override string GetPreferredFileName()
        {
            if (Name != null)
                return Name;
            else
                return "";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(16, SeekOrigin.Current);
            Order = Read2(s);
            Name = ReadString(s);
            int dataSize = (int)size - 18 - (Name.Length + 1);
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.InstallerSh);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            Write2(s, Order);
            WriteString(s, Name);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgKernel2 : PkgChunk
    {
        private byte[] data;

        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public override string AsString { get { return "Kernel 2"; } }

        public override string GetPreferredFileName()
        {
            return "uImage2";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(16, SeekOrigin.Current);
            int dataSize = (int)size - 16;
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.Kernel2);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgDirectory : PkgChunk
    {
        private ushort permissions;
        private string name;

        public ushort Permissions
        {
            get { return permissions; }
            set { SetField(ref permissions, value); OnPropertyChanged("AsString"); }
        }
        public string Name
        {
            get { return name; }
            set { SetField(ref name, value); }
        }

        public override string AsString { get { return "Directory - " + Name; } }

        public override string GetPreferredFileName()
        {
            throw new NotImplementedException();
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(4, SeekOrigin.Current);
            Permissions = Read2(s);
            s.Seek(2, SeekOrigin.Current);
            Name = ReadString(s);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.Directory);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            WriteArray(s, new byte[] { 0x00, 0x00, 0x00, 0x00 });
            Write2(s, Permissions);
            WriteArray(s, new byte[] { 0xFF, 0xFF });
            WriteString(s, Name);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            throw new NotImplementedException();
        }
    }

    public class PkgFile : PkgChunk
    {
        private ushort permissions;
        private ushort unknown;
        private bool isCompressed;
        private string name;
        private string date;
        private string time;
        private byte[] data;

        public ushort Permissions
        {
            get { return permissions; }
            set { SetField(ref permissions, value); }
        }
        public ushort Unknown
        {
            get { return unknown; }
            set { SetField(ref unknown, value); }
        }
        public bool IsCompressed
        {
            get { return isCompressed; }
            set { SetField(ref isCompressed, value); }
        }
        public string Name
        {
            get { return name; }
            set { SetField(ref name, value); OnPropertyChanged("AsString"); }
        }
        public string Date
        {
            get { return date; }
            set { SetField(ref date, value); }
        }
        public string Time
        {
            get { return time; }
            set { SetField(ref time, value); }
        }
        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }
        public override string AsString { get { return "File - " + Name; } }

        public PkgFile()
        {
            Unknown = 0xFFFF;
        }

        public override string ToString()
        {
            return "File - " + Name;
        }

        public override string GetPreferredFileName()
        {
            if (Name != null)
                return Name.Substring(Name.LastIndexOf('/') + 1);
            else
                return "";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(20, SeekOrigin.Current);
            Permissions = Read2(s);
            Unknown = Read2(s); //s.Seek(2, SeekOrigin.Current);
            uint dataSize = Read4(s);
            if (Read1(s) == 0)
                IsCompressed = false;
            else
                IsCompressed = true;
            Name = ReadString(s);
            Date = ReadString(s);
            Time = ReadString(s);

            if (IsCompressed)
            {
                Data = new byte[dataSize];

                int index = 0;

                while (true)
                {
                    uint blockType = Read4(s);
                    if (blockType != 0x00000100)
                        break;
                    uint compressedBlockSize = Read4(s) - 4;
                    uint uncompressedBlockSize = Swap4(Read4(s));
                    byte[] compressed = ReadArray(s, (int)compressedBlockSize);
                    byte[] uncompressed = ZLibNet.ZLibCompressor.DeCompress(compressed);
                    Array.Copy(uncompressed, 0, Data, index, uncompressedBlockSize);
                    index += (int)uncompressedBlockSize;
                    if ((compressedBlockSize % 4) != 0)
                        s.Seek(4 - (compressedBlockSize % 4), SeekOrigin.Current);
                }
            } else {
                Data = ReadArray(s, (int)dataSize);
            }
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, tempOffset;
            Write4(s, (uint)Id.File);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            WriteArray(s, new byte[] { 0x00, 0x00, 0x00, 0x00 });
            Write2(s, Permissions);
            Write2(s, Unknown); //WriteArray(s, new byte[] { 0xFF, 0xFF });
            Write4(s, (uint)Data.Length);
            Write1(s, (byte)(IsCompressed ? 1 : 0));
            WriteString(s, Name);
            WriteString(s, Date);
            WriteString(s, Time);

            if (IsCompressed == true)
            {
                int index = 0;
                int reaming = (int)Data.Length;
                if (Data.Length > 0)
                {
                    do
                    {
                        int blockSize = 0;

                        if (reaming > 0x00100000)
                        {
                            blockSize = 0x00100000;
                        } else {
                            blockSize = reaming;
                        }

                        MemoryStream compressed = new MemoryStream();
                        ZLibNet.ZLibStream compressor = new ZLibNet.ZLibStream(compressed, ZLibNet.CompressionMode.Compress, ZLibNet.CompressionLevel.BestCompression);
                        compressor.Write(Data, index, blockSize);
                        compressor.Flush();
                        byte[] compressedArray = new byte[compressed.Length];
                        compressed.Seek(0, SeekOrigin.Begin);
                        compressed.Read(compressedArray, 0, (int)compressed.Length);

                        Write4(s, 0x00000100);
                        Write4(s, (uint)(compressed.Length + 4));
                        Write4(s, Swap4((uint)blockSize));
                        WriteArray(s, compressedArray);

                        if ((compressed.Length % 4) != 0)
                            s.Seek(4 - (compressed.Length % 4), SeekOrigin.Current);

                        index += 0x00100000;
                        reaming -= 0x00100000;
                    } while (index < Data.Length);
                }

                Write4(s, 0x00000101);
                Write4(s, 0x00000000);
            } else {
                WriteArray(s, Data);
            }

            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public void ImportDataFromStream(Stream s)
        {
            Data = ReadArray(s, (int)s.Length);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }

    public class PkgFileSystem : PkgChunk
    {
        private string name;
        private byte[] data;

        public string Name
        {
            get { return name; }
            set { SetField(ref name, value); OnPropertyChanged("AsString"); }
        }
        public byte[] Data
        {
            get { return data; }
            set { SetField(ref data, value); }
        }

        public override string AsString { get { return "FileSystem - " + Name; } }

        public override string GetPreferredFileName()
        {
            if (Name != null)
                return Name.Substring(Name.LastIndexOf('/') + 1);
            else
                return "";
        }

        public override void LoadChunkFromStream(Stream s, int size)
        {
            s.Seek(22, SeekOrigin.Current);
            Name = ReadString(s);
            int dataSize = (int)size - 22 - (Name.Length + 1);
            Data = ReadArray(s, dataSize);
        }

        public override void SaveChunkToStream(Stream s)
        {
            long sizeOffset, size2Offset, tempOffset;
            Write4(s, (uint)Id.FileSystem);
            sizeOffset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            byte[] hash = MD5.Create().ComputeHash(Data);
            WriteArray(s, hash);
            size2Offset = s.Position;
            s.Seek(4, SeekOrigin.Current);
            WriteArray(s, new byte[] { 0x02, 0x00 });
            WriteString(s, Name);
            WriteArray(s, Data);
            tempOffset = s.Position;
            uint size = (uint)(tempOffset - sizeOffset - 4);
            s.Seek(sizeOffset, SeekOrigin.Begin);
            Write4(s, size);
            s.Seek(size2Offset, SeekOrigin.Begin);
            Write4(s, size & 0xFFFFFF00);
            s.Seek(tempOffset, SeekOrigin.Begin);
        }

        public override void ExportDataToStream(Stream s)
        {
            WriteArray(s, Data);
        }
    }
}
