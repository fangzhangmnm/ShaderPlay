using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using System;
namespace fzmnm
{
    //credits https://gist.github.com/distantcam/64cf44d84441e5c45e197f7d90c6df3e
    //usage await job.Schedule(length, batchSize); //will also Complete() the jobHandle
    public class JobHandleAwaiter : INotifyCompletion
    {
        readonly JobHandle jobHandle;

        public JobHandleAwaiter(JobHandle jobHandle)
        {
            this.jobHandle = jobHandle;
        }
        public JobHandleAwaiter GetAwaiter() => this;
        public bool IsCompleted => jobHandle.IsCompleted;
        public void OnCompleted(Action continuation)
        {
            jobHandle.Complete();
            continuation();
        }

        public void GetResult() { }
    }
    public static class JobHandleAwiterUtil
    {
        public static JobHandleAwaiter GetAwaiter(this JobHandle jobHandle) => new JobHandleAwaiter(jobHandle);
    }

}
