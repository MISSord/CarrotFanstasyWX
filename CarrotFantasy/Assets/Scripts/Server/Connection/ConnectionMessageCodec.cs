using ETModel;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CarrotFantasy
{
    public static class ConnectionMessageCodec
    {
        private static readonly Dictionary<Type, ushort> typeToOpcode = new Dictionary<Type, ushort>();
        private static readonly Dictionary<ushort, Type> opcodeToType = new Dictionary<ushort, Type>();
        private static bool isLoaded = false;

        public static bool TryGetOpcode(IMessage message, out ushort opcode)
        {
            opcode = 0;
            if (message == null)
            {
                return false;
            }

            ensureLoaded();
            return typeToOpcode.TryGetValue(message.GetType(), out opcode);
        }

        public static bool TryDecode(ushort opcode, byte[] payload, out IMessage message)
        {
            message = null;
            ensureLoaded();

            if (!opcodeToType.TryGetValue(opcode, out Type messageType))
            {
                return false;
            }

            object value = ProtobufHelper.FromBytes(messageType, payload, 0, payload.Length);
            message = value as IMessage;
            return message != null;
        }

        public static byte[] EncodePacket(ushort opcode, IMessage message)
        {
            byte[] body = ProtobufHelper.ToBytes(message);
            byte[] packet = new byte[2 + body.Length];
            byte[] opcodeBytes = BitConverter.GetBytes(opcode);

            packet[0] = opcodeBytes[0];
            packet[1] = opcodeBytes[1];
            Buffer.BlockCopy(body, 0, packet, 2, body.Length);
            return packet;
        }

        public static bool TryDecodePacket(byte[] packet, out ushort opcode, out IMessage message)
        {
            opcode = 0;
            message = null;

            if (packet == null || packet.Length < 2)
            {
                return false;
            }

            opcode = BitConverter.ToUInt16(packet, 0);
            int bodyLen = packet.Length - 2;
            byte[] payload = new byte[bodyLen];
            Buffer.BlockCopy(packet, 2, payload, 0, bodyLen);
            return TryDecode(opcode, payload, out message);
        }

        private static void ensureLoaded()
        {
            if (isLoaded)
            {
                return;
            }

            isLoaded = true;
            typeToOpcode.Clear();
            opcodeToType.Clear();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null)
                {
                    continue;
                }

                for (int j = 0; j < types.Length; j++)
                {
                    Type type = types[j];
                    if (type == null)
                    {
                        continue;
                    }

                    object[] attrs = type.GetCustomAttributes(typeof(MessageAttribute), false);
                    if (attrs == null || attrs.Length == 0)
                    {
                        continue;
                    }

                    MessageAttribute attr = attrs[0] as MessageAttribute;
                    if (attr == null)
                    {
                        continue;
                    }

                    if (!typeof(IMessage).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    if (!typeToOpcode.ContainsKey(type))
                    {
                        typeToOpcode.Add(type, attr.Opcode);
                    }

                    if (!opcodeToType.ContainsKey(attr.Opcode))
                    {
                        opcodeToType.Add(attr.Opcode, type);
                    }
                }
            }
        }
    }
}
