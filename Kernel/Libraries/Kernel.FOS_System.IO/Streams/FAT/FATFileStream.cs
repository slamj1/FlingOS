﻿using System;

using Kernel.FOS_System.Collections;
using Kernel.FOS_System.IO.FAT;

namespace Kernel.FOS_System.IO.Streams.FAT
{
    public class FATFileStream : FileStream
    {
        protected UInt32List ClusterNums;

        public FATFileSystem TheFATFileSystem
        {
            get
            {
                return (FATFileSystem)TheFile.TheFileSystem;
            }
        }
        public FATFile TheFATFile
        {
            get
            {
                return (FATFile)TheFile;
            }
        }

        protected UInt64 mPosition = 0;
        public override long Position
        {
            get
            {
                return (long)mPosition;
            }
            set
            {
                if (value < 0L)
                {
                    ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Position value must be > 0!"));
                }
                mPosition = (ulong)value;
            }
        }

        public bool IgnoreFileSize = false;
        public UInt64 GetActualSize()
        {
            if (IgnoreFileSize)
            {
                if (ClusterNums == null)
                {
                    GetClusterNums();
                }
                return (uint)ClusterNums.Count * TheFATFileSystem.BytesPerCluster;
            }
            return theFile.Size;
        }

        public FATFileStream(FATFile aFile, bool ignoreFileSize)
            : base(aFile)
        {
            IgnoreFileSize = ignoreFileSize;

            if (TheFATFile == null)
            {
                ExceptionMethods.Throw(new Exception("Could not create FATFileStream. Specified file object was null!"));
            }

            GetClusterNums();
        }

        private void GetClusterNums()
        {
            if (TheFATFile.FirstClusterNum > 0 || IgnoreFileSize)
            {
                //BasicConsole.WriteLine("Reading cluster chain...");
                ClusterNums = TheFATFileSystem.ReadClusterChain(TheFile.Size, TheFATFile.FirstClusterNum);
                //BasicConsole.WriteLine("Read cluster chain.");
            }
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (TheFile.Size > 0 || IgnoreFileSize)
            {
                if (ClusterNums == null)
                {
                    GetClusterNums();
                    if(ClusterNums == null)
                    {
                        return 0;
                    }
                }

                FATFileSystem mFS = (FATFileSystem)TheFile.TheFileSystem;
                FATFile mFile = TheFATFile;

                //BasicConsole.WriteLine("Checking params...");

                if (count < 0)
                {
                    ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Read: aCount must be > 0"));
                }
                else if (offset < 0)
                {
                    ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Read: anOffset must be > 0"));
                }
                else if (buffer == null)
                {
                    ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Read: aBuffer must not be null!"));
                }
                else if (buffer.Length - offset < count)
                {
                    ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Read: Invalid offset / length values!"));
                }
                else if (mFile.FirstClusterNum == 0)
                {
                    // FirstSector can be 0 for 0 length files
                    return 0;
                }
                else if (!IgnoreFileSize && mPosition == mFile.Size)
                {
                    // EOF
                    return 0;
                }

                //BasicConsole.WriteLine("Params OK.");
                                
                // Reduce count, so that no out of bounds exceptions occur
                ulong fileSize = 0;
                if (IgnoreFileSize)
                {
                    fileSize = (ulong)ClusterNums.Count * TheFATFileSystem.BytesPerCluster;
                }
                else
                {
                    fileSize = mFile.Size;
                }
                ulong xMaxReadableBytes = fileSize - mPosition;
                ulong xCount = (ulong)count;
                if (xCount > xMaxReadableBytes)
                {
                    xCount = xMaxReadableBytes;
                }

                //BasicConsole.WriteLine("Creating new cluster array...");

                byte[] xCluster = mFS.NewClusterArray();
                UInt32 xClusterSize = mFS.BytesPerCluster;

                int read = 0;

                //BasicConsole.WriteLine("Reading data...");

                while (xCount > 0)
                {
                    UInt32 xClusterIdx = (UInt32)mPosition / xClusterSize;
                    UInt32 xPosInCluster = (UInt32)mPosition % xClusterSize;
                    mFS.ReadCluster(ClusterNums[(int)xClusterIdx], xCluster);
                    uint xReadSize;
                    if (xPosInCluster + xCount > xClusterSize)
                    {
                        xReadSize = (xClusterSize - xPosInCluster - 1);
                    }
                    else
                    {
                        xReadSize = (uint)xCount;
                    }

                    // TODO: Should we do an argument check here just in case?
                    FOS_System.Array.Copy(xCluster, (int)xPosInCluster, buffer, offset, (int)xReadSize);
                    offset += (int)xReadSize;
                    xCount -= (ulong)xReadSize;
                    read += (int)xReadSize;
                }

                mPosition += (ulong)offset;
                return read;
            }
            else
            {
                return 0;
            }
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count < 0)
            {
                ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Write: aCount must be > 0"));
            }
            else if (offset < 0)
            {
                ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Write: anOffset must be > 0"));
            }
            else if (buffer == null)
            {
                ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Write: aBuffer must not be null!"));
            }
            else if (buffer.Length - offset < count)
            {
                ExceptionMethods.Throw(new Exceptions.ArgumentException("FATFileStream.Write: Invalid offset / length values!"));
            }

            //BasicConsole.WriteLine("Checks passed.");

            FATFileSystem mFS = (FATFileSystem)TheFile.TheFileSystem;
            FATFile mFile = TheFATFile;

            if (ClusterNums == null)
            {
                //BasicConsole.WriteLine("Getting cluster nums...");

                GetClusterNums();
                if (ClusterNums == null)
                {
                    //BasicConsole.WriteLine("Failed to get cluster nums.");
                    return;
                }

                //BasicConsole.WriteLine("Got cluster nums.");
            }

            //BasicConsole.WriteLine("Creating write buffer...");

            UInt32 xClusterSize = mFS.BytesPerCluster;
            byte[] writeBuffer = mFS.NewClusterArray();

            //BasicConsole.WriteLine("Writing data...");

            while(count > 0)
            {
                UInt32 clusterIdx = (UInt32)mPosition / xClusterSize;
                UInt32 posInCluster = (UInt32)mPosition % xClusterSize;

                bool newCluster = false;
                while (clusterIdx >= ClusterNums.Count)
                {
                    //BasicConsole.WriteLine("Expanding clusters...");

                    UInt32 lastClusterNum = ClusterNums[ClusterNums.Count];
                    UInt32 nextClusterNum = mFS.GetNextFreeCluster(lastClusterNum);

                    //Clear cluster
                    mFS.WriteCluster(nextClusterNum, null);

                    //Set last FAT entry to point to next cluster
                    mFS.SetFATEntryAndSave(lastClusterNum, nextClusterNum);

                    //Set next cluster as EOF
                    mFS.SetFATEntryAndSave(nextClusterNum, FATFileSystem.GetFATEntryEOFValue(mFS.FATType));

                    //Add next cluster num to our list
                    ClusterNums.Add(nextClusterNum);
                    newCluster = true;
                }

                if((posInCluster != 0 || count < xClusterSize) && !newCluster)
                {
                    //BasicConsole.WriteLine("Reading existing data...");

                    mFS.ReadCluster(ClusterNums[(int)clusterIdx], writeBuffer);

                    //BasicConsole.WriteLine("Read existing data.");
                }

                //BasicConsole.WriteLine("Calculating write size...");
                int writeSize = count < (xClusterSize - posInCluster) ? count : 
                                            (int)(xClusterSize - posInCluster);
                //BasicConsole.WriteLine("Calculated write size. Copying data to write...");
                Array.Copy(buffer, offset, writeBuffer, (int)posInCluster, writeSize);
                //BasicConsole.WriteLine("Data copied. Writing data to disk...");

                mFS.WriteCluster(ClusterNums[(int)clusterIdx], writeBuffer);

                //BasicConsole.WriteLine("Written data.");

                count -= writeSize;
                offset += writeSize;
                mPosition += (uint)writeSize;
            }

            //BasicConsole.WriteLine("Write completed.");

            if (!IgnoreFileSize)
            {
                if (mPosition > mFile.Size)
                {
                    //Update file info
                    mFile.Size = mPosition;
                    //Update directory entry
                    mFile.Parent.WriteListings();
                }
            }
        }
    }
}