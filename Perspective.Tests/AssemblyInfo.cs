using Xunit;

// 符号计算型测试彼此争抢 CPU 会让耗时不稳定，这里关闭并行。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
