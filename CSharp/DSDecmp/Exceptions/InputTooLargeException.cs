using System;
using System.Collections.Generic;
using System.Text;

namespace DSDecmp
{
    public class InputTooLargeException : Exception
    {
        public InputTooLargeException()
            : base("The compression ratio is not high enough to fit the input "
            + "in a single compressed file.") { }
    }
}
