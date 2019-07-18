using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Lambda
{
    /// <summary>
    /// Represents a message context, the headers etc.
    /// </summary>
    public class MessageContext
    {
        public Dictionary<string, string> Headers { get; set; }
    }
}
