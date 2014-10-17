using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baker
{
    /// <summary>
    /// Contract that defines a processor.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    public interface IProcessor<TInput, TOutput>
    {
        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        TOutput Process(TInput input);
    }

    /// <summary>
    /// Represents a processor base class.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    public abstract class ProcessorBase<TInput, TOutput> : Pipeline<TInput, TOutput>, IProcessor<TInput, TOutput>
    {
        /// <summary>
        /// Constructs a new instance of a processor.
        /// </summary>
        public ProcessorBase() : base(32)
        {
            this.Executor = this.Process;
        }

        /// <summary>
        /// Constructs a new instance of a processor.
        /// </summary>
        /// <param name="degreeOfParallelism">Degree of parallelism for this procesor.</param>
        public ProcessorBase(int degreeOfParallelism)
            : base(degreeOfParallelism)
        {
            this.Executor = this.Process;
        }

        /// <summary>
        /// Processes a single item.
        /// </summary>
        /// <param name="input">The input to process.</param>
        /// <returns>The output of the process.</returns>
        public abstract TOutput Process(TInput input);


    }
}
