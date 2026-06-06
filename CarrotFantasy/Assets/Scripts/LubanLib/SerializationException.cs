using System;

namespace Luban
{
    public sealed class SerializationException : Exception
    {
        public SerializationException()
        {
        }

        public SerializationException(string message) : base(message)
        {
        }
    }
}
