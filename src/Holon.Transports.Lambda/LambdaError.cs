using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports.Lambda
{
    /// <summary>
    /// Represents a lambda error.
    /// </summary>
    class LambdaError
    {
        [JsonProperty("errorMessage")]
        public object ErrorMessage { get; set;}
    }
}
