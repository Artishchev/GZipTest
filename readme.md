# GZipTestApp
## Task
Develop a C# console application for compressing and decompressing files block-by-block using System.IO.Compression.GzipStream.
The source file is divided into blocks of the same size for compression. For example 1 megabyte blocks. Each block is compressed and written to the output file independently of the rest of the blocks.
The program must effectively parallelize and synchronize the processing of blocks in a multiprocessor environment and be able to process files that are larger than the amount of available RAM.
In case of exceptions it is necessary to inform the user with a clear message that allows the user to correct arised problem. In particular, if the problems are related to the limitations of the operating system.
The program code must comply with the principles of OOP and OOD (readability, division into classes, etc.).
Program parameters, names of source and result files must be specified in the command line as follows:
`GZipTest.exe compress / decompress [source file name] [target file name]`
On success the program should return 0. On error return 1.
Note: the format of the archive is left to the discretion of the author, and does not matter for assessing the quality of the test. In particular, compliance with the GZIP format is optional.

## Solution
#### Distinctive features
 - **MemoryPool** - usage of MemoryOwner ([Microsoft.Toolkit.HighPerformance.Buffers](https://docs.microsoft.com/en-us/windows/communitytoolkit/high-performance/memoryowner)) prevent GC performance problem and large objects allocation.
 - **DataFlow** - helps make code more readable and make it easier to manage threads
#### ToDo
:white_check_mark: Asynchronous compression
 
:white_check_mark: Asynchronous decompression

:white_check_mark: Backward compatibility with original GZip file format

:white_check_mark: Comments and description

:white_check_mark: Clear messages to the user

:white_check_mark: Exception Handling

:white_check_mark: Graceful cancelation 

:white_check_mark: Progress bar

:white_large_square: Asynchronous GZip chunks determination

:white_large_square: Unit tests

[Other implementations](https://github.com/search?o=desc&q=GZipTest&s=updated&type=Repositories&utf8=%E2%9C%93)
