using System;
using System.Collections.Generic;
using System.Threading;

namespace CiaiControllerSDK.Models
{
    public sealed class DriverEvent : EventArgs
    {
        public string Type { get; set; }
        public string InstructionId { get; set; }
        public string NestId { get; set; }
        public double? Progress { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class DriverExecutionContext
    {
        internal DriverExecutionContext(string instructionId, string nestId, CancellationToken token)
        { InstructionId = instructionId; NestId = nestId; CancellationToken = token; }
        public string InstructionId { get; }
        public string NestId { get; }
        public CancellationToken CancellationToken { get; }
    }
}
