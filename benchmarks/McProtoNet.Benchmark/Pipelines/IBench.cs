﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace McProtoNet.Benchmark.Pipelines;

public interface IBench 
{
    Task Setup(Stream stream, int compressionThreshold);
    
    Task Run(int packetsCount);
    
    Task Cleanup();
}