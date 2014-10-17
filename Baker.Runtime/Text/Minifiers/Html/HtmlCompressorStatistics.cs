namespace Baker.Text
{
	using System;

	public sealed class HtmlCompressorStatistics
	{
		private HtmlMetrics originalMetrics = new HtmlMetrics();
		private HtmlMetrics compressedMetrics = new HtmlMetrics();
		private long time = 0;
		private int preservedSize = 0;

		/**
		 * Returns metrics of an uncompressed document
		 * 
		 * @return metrics of an uncompressed document
		 * @see HtmlMetrics
		 */
		public HtmlMetrics getOriginalMetrics()
		{
			return originalMetrics;
		}

		/**
		 * @param originalMetrics the originalMetrics to set
		 */
		public void setOriginalMetrics(HtmlMetrics originalMetrics)
		{
			this.originalMetrics = originalMetrics;
		}

		/**
		 * Returns metrics of a compressed document
		 * 
		 * @return metrics of a compressed document
		 * @see HtmlMetrics
		 */
		public HtmlMetrics getCompressedMetrics()
		{
			return compressedMetrics;
		}

		/**
		 * @param compressedMetrics the compressedMetrics to set
		 */
		public void setCompressedMetrics(HtmlMetrics compressedMetrics)
		{
			this.compressedMetrics = compressedMetrics;
		}

		/**
		 * Returns total compression time. 
		 * 
		 * <p>Please note that compression performance varies very significantly depending on whether it was 
		 * a cold run or not (specifics of Java VM), so for accurate real world results it is recommended 
		 * to take measurements accordingly.   
		 * 
		 * @return the compression time, in milliseconds 
		 *      
		 */
		public long getTime()
		{
			return time;
		}

		/**
		 * @param time the time to set
		 */
		public void setTime(long time)
		{
			this.time = time;
		}

		/**
		 * Returns total size of blocks that were skipped by the compressor 
		 * (for example content inside <code>&lt;pre></code> tags or inside   
		 * <code>&lt;script></code> tags with disabled javascript compression)
		 * 
		 * @return the total size of blocks that were skipped by the compressor, in bytes
		 */
		public int getPreservedSize()
		{
			return preservedSize;
		}

		/**
		 * @param preservedSize the preservedSize to set
		 */
		public void setPreservedSize(int preservedSize)
		{
			this.preservedSize = preservedSize;
		}

		public override string ToString()
		{
			return String.Format("Time={0}, Preserved={1}, Original={2}, Compressed={3}", time, preservedSize, originalMetrics, compressedMetrics);
		}
	}
}