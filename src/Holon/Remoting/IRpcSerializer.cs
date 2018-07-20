using System;
using System.Collections.Generic;
using System.Text;

namespace Holon.Remoting
{
    /// <summary>
    /// Resolves the signature for an interface operation.
    /// </summary>
    /// <param name="interface">The interface.</param>
    /// <param name="operation">The operation.</param>
    /// <returns></returns>
    public delegate RpcArgument[] RpcSignatureResolver(string @interface, string operation);

    /// <summary>
    /// Defines the interface for serializing and deserializing RPC request/response payloads.
    /// </summary>
    internal interface IRpcSerializer
    {
        string Name { get; }

        RpcRequest DeserializeRequest(byte[] body, RpcSignatureResolver resolver);
        RpcRequest[] DeserializeRequestBatch(byte[] body, RpcSignatureResolver resolver);
        RpcResponse DeserializeResponse(byte[] body, Type responseType);
        RpcResponse[] DeserializeResponseBatch(byte[] body, Type[] responseTypes);

        byte[] SerializeRequest(RpcRequest request);
        byte[] SerializeRequestBatch(RpcRequest[] batch);
        byte[] SerializeResponse(RpcResponse response);
        byte[] SerializeResponseBatch(RpcResponse[] batch);
    }
}
