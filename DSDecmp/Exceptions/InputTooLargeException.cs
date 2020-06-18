﻿using System;
using System.Collections.Generic;
using System.Text;

namespace DSDecmp
{
    /// <summary>
    /// An exception indicating that the file cannot be compressed, because the decompressed size
    /// cannot be represented in the current compression format.
    /// </summary>
    public class InputTooLargeException : Exception
    {
        /// <summary>
        /// Creates a new exception that indicates that the input is too big to be compressed.
        /// </summary>
        public InputTooLargeException()
            : base("The compression ratio is not high enough to fit the input "
            + "in a single compressed file.") { }
    }
}
