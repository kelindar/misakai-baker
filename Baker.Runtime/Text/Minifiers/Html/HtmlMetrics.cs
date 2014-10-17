namespace Baker.Text
{
	public sealed class HtmlMetrics
	{
		private int filesize = 0;
		private int emptyChars = 0;
		private int inlineScriptSize = 0;
		private int inlineStyleSize = 0;
		private int inlineEventSize = 0;

		/**
		 * Returns total filesize of a document
		 * 
		 * @return total filesize of a document, in bytes
		 */

		public int getFilesize()
		{
			return filesize;
		}

		/**
		 * @param filesize the filesize to set
		 */

		public void setFilesize(int filesize)
		{
			this.filesize = filesize;
		}

		/**
		 * Returns number of empty characters (spaces, tabs, end of lines) in a document
		 * 
		 * @return number of empty characters in a document
		 */

		public int getEmptyChars()
		{
			return emptyChars;
		}

		/**
		 * @param emptyChars the emptyChars to set
		 */

		public void setEmptyChars(int emptyChars)
		{
			this.emptyChars = emptyChars;
		}

		/**
		 * Returns total size of inline <code>&lt;script></code> tags
		 * 
		 * @return total size of inline <code>&lt;script></code> tags, in bytes
		 */

		public int getInlineScriptSize()
		{
			return inlineScriptSize;
		}

		/**
		 * @param inlineScriptSize the inlineScriptSize to set
		 */

		public void setInlineScriptSize(int inlineScriptSize)
		{
			this.inlineScriptSize = inlineScriptSize;
		}

		/**
		 * Returns total size of inline <code>&lt;style></code> tags
		 * 
		 * @return total size of inline <code>&lt;style></code> tags, in bytes
		 */

		public int getInlineStyleSize()
		{
			return inlineStyleSize;
		}

		/**
		 * @param inlineStyleSize the inlineStyleSize to set
		 */

		public void setInlineStyleSize(int inlineStyleSize)
		{
			this.inlineStyleSize = inlineStyleSize;
		}

		/**
		 * Returns total size of inline event handlers (<code>onclick</code>, etc)
		 * 
		 * @return total size of inline event handlers, in bytes
		 */

		public int getInlineEventSize()
		{
			return inlineEventSize;
		}

		/**
		 * @param inlineEventSize the inlineEventSize to set
		 */

		public void setInlineEventSize(int inlineEventSize)
		{
			this.inlineEventSize = inlineEventSize;
		}

		public override string ToString()
		{
			return string.Format(
				"Filesize={0}, Empty Chars={1}, Script Size={2}, Style Size={3}, Event Handler Size={4}",
				filesize, emptyChars, inlineScriptSize, inlineStyleSize, inlineEventSize);
		}
	}
}