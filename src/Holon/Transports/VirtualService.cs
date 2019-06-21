using Holon.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Transports
{
    public class VirtualService : Service
    {
        public VirtualService(Transport transport, ServiceAddress addr, ServiceBehaviour behaviour, ServiceConfiguration configuration) : base(transport, addr, behaviour, configuration) {
        }
    }
}
