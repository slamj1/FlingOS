﻿using System;
using Kernel.FOS_System.IO;
using Kernel.Hardware.Processes;
using Kernel.Core.Processes.ELF;

namespace Kernel.Core.Processes
{
    public static unsafe class DynamicLinkerLoader
    {
        public static Process LoadProcess_FromRawExe(File RawExeFile, bool UserMode)
        {
            // - Read in file contents
            // - Map enough memory for the exe file contents
            // - Copy the memory over

            bool reenable = Scheduler.Enabled;
            if (reenable)
            {
                Scheduler.Disable();
            }

            //TODO - Handle case of EXE being bigger than 4KiB?
            //          - Would need to map contiguous (virtual) pages.
            
            //BasicConsole.WriteLine("Mapping free page for process...");
            byte* destMemPtr = (byte*)Hardware.VirtMemManager.MapFreePage(
                UserMode ? Hardware.VirtMem.VirtMemImpl.PageFlags.None : Hardware.VirtMem.VirtMemImpl.PageFlags.KernelOnly);
            //BasicConsole.WriteLine(((FOS_System.String)"destMemPtr=") + (uint)destMemPtr);

            // Add the page to the current processes memory layout
            //  So we can access the memory while we load the process. 
            //  Otherwise we will hit a page fault as soon as we try copying the memory
            //      further down.
            //  Note: The page fault will only be hit on starting a second process because
            //      the scheduler doesn't change the context when only one process is running.
            ProcessManager.CurrentProcess.TheMemoryLayout.AddDataPage(
                (uint)Hardware.VirtMemManager.GetPhysicalAddress(destMemPtr),
                (uint)destMemPtr);

            //BasicConsole.WriteLine("Reading file...");
            int bytesRead = 0;
            byte[] readBuffer = new byte[4096];
            bytesRead = RawExeFile.GetStream().Read(readBuffer, 0, 4096);
            //BasicConsole.WriteLine("Copying data...");
            Utilities.MemoryUtils.MemCpy_32(destMemPtr, ((byte*)Utilities.ObjectUtilities.GetHandle(readBuffer)) + FOS_System.Array.FieldsBytesSize, (uint)bytesRead);

            //BasicConsole.WriteLine("Converting destMemPtr...");
            ThreadStartMethod mainMethod = (ThreadStartMethod)Utilities.ObjectUtilities.GetObject(destMemPtr);

            //BasicConsole.WriteLine("Getting process name...");
            FOS_System.String name = RawExeFile.Name;

            // - Create the process
            //BasicConsole.WriteLine("Creating process...");
            Process process = ProcessManager.CreateProcess(
                mainMethod, name, UserMode);
            //BasicConsole.WriteLine("Adding process' code page...");
            // Add code page to new processes memory layout
            process.TheMemoryLayout.AddCodePage((uint)Hardware.VirtMemManager.GetPhysicalAddress(destMemPtr),
                (uint)destMemPtr);

            //BasicConsole.WriteLine("Removing process' code page from current process...");
            //Remove from current processes memory layout
            ProcessManager.CurrentProcess.TheMemoryLayout.RemovePage((uint)destMemPtr);

            if (reenable)
            {
                Scheduler.Enable();
            }

            return process;
        }
        public static Process LoadProcess_FromELFExe(File RawExeFile, bool UserMode)
        {
            ELFFile elfFile = new ELFFile(RawExeFile);
            #region CHECK : Signature
            if (!elfFile.CheckSiganture())
            {
                Console.Default.WarningColour();
                Console.Default.WriteLine("ELF signature check failed!");
                Console.Default.DefaultColour();
                return null;
            }
            else
            {
                Console.Default.Colour(0x2F);
                Console.Default.WriteLine("ELF signature check passed.");
                Console.Default.DefaultColour();
            }
            #endregion
            #region CHECK : File Class
            if (!elfFile.CheckFileClass())
            {
                Console.Default.WarningColour();
                Console.Default.WriteLine("ELF file class check failed!");
                Console.Default.DefaultColour();
                return null;
            }
            else
            {
                Console.Default.Colour(0x2F);
                Console.Default.WriteLine("ELF file class check passed.");
                Console.Default.DefaultColour();
            }
            #endregion
            #region CHECK : Data Encoding
            if (!elfFile.CheckDataEncoding())
            {
                Console.Default.WarningColour();
                Console.Default.WriteLine("ELF data encoding check failed!");
                Console.Default.DefaultColour();
                return null;
            }
            else
            {
                Console.Default.Colour(0x2F);
                Console.Default.WriteLine("ELF data encoding check passed.");
                Console.Default.DefaultColour();
            }
            #endregion
            #region CHECK : File Type
            if (!elfFile.CheckFileType())
            {
                Console.Default.WarningColour();
                Console.Default.WriteLine("ELF file type check failed!");
                Console.Default.DefaultColour();
                return null;
            }
            else
            {
                Console.Default.Colour(0x2F);
                Console.Default.WriteLine("ELF file type check passed.");
                Console.Default.DefaultColour();
            }
            #endregion
            #region CHECK : Machine
            if (!elfFile.CheckMachine())
            {
                Console.Default.WarningColour();
                Console.Default.WriteLine("ELF machine check failed!");
                Console.Default.DefaultColour();
                return null;
            }
            else
            {
                Console.Default.Colour(0x2F);
                Console.Default.WriteLine("ELF machine check passed.");
                Console.Default.DefaultColour();
            }
            #endregion
            #region INFO : Header Version
            Console.Default.Write("ELF Header version: ");
            Console.Default.WriteLine_AsDecimal(elfFile.Header.HeaderVersion);
            #endregion
            

            return null;
        }
    }
}