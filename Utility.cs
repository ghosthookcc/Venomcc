﻿using System;
using System.Text;
using Venomcc.Networking;
using Venomcc.ICommand;

namespace Venomcc.Utility
{
    public static class GeneralUtilities
    {
        public static bool IsNull<T>(T inObj)
        {
            return inObj == null ? true : false;
        }
        public static int ReplaceFirst(ref string data, string replaceTerm, string replaceWithTerm)
        {
            int positionFirstOccurance = data.IndexOf(replaceTerm);
            data = data.Substring(0, positionFirstOccurance) + replaceWithTerm + data.Substring(positionFirstOccurance + replaceTerm.Length);
            return positionFirstOccurance;
        }
    }
}

namespace Venomcc.Utility.Networking
{
    public static class NetworkUtilities
    {
        public static List<string> parseMessageAsCommand(ICommand.ICommand command, string data)
        {
            List<(string tag, int startOffset, int endOffset)> dataChunks = parseMessagesHeaderTags(data);

            return new List<string>();
        }

        public static string parseMessageIgnoringHeaderTags(string data)
        {
            foreach (KeyValuePair<string, string> KVP in NetUser.MessageHeaderTags)
            {
                data = data.Replace(KVP.Value, "");
            }
            return data;
        }

        public static List<(string tag, int startOffset, int endOffset)> parseMessagesHeaderTags(string data)
        {
            List<(string tag, int startOffset, int endOffset)> tags = new List<(string tag, int startOffset, int endOffset)>();

            string tag;
            int messageOffset = 0;

            for (int idx = 0; idx < data.Length; idx++)
            {
                foreach (KeyValuePair<string, string> KVP in NetUser.MessageHeaderTags)
                {
                    if (idx + messageOffset + KVP.Value.Length > data.Length) continue;

                    tag = data.Substring(idx + messageOffset, KVP.Value.Length);
                    if (NetUser.MessageHeaderTags.ContainsValue(tag))
                    {
                        tags.Add((tag, idx + messageOffset, idx + messageOffset + KVP.Value.Length));
                        messageOffset += tag.Length;
                    }
                }
            }
            return tags;
        }

        public static List<byte[]> generateDataChunksWithHeaderTags(string data, ushort chunkSize)
        {
            if (data.Length > 0)
            {
                int leftover = data.Length;
                int newStartIdx = 0;

                List<byte[]> dataChunks = new List<byte[]>();

                string dataChunk;
                string addedTag;
                while (leftover > chunkSize)
                {
                    addedTag = NetUser.MessageHeaderTags["CCD"];
                    dataChunk = data.Substring(newStartIdx, chunkSize - addedTag.Length);
                    dataChunks.Add(Encoding.UTF8.GetBytes(dataChunk + addedTag));
                    newStartIdx += dataChunk.Length;
                    leftover -= dataChunk.Length;
                }
                dataChunks.Add(Encoding.UTF8.GetBytes(data.Substring(newStartIdx, leftover)));

                int totalLength = 0;
                for (int idx = 0; idx < dataChunks.Count(); idx++)
                {
                    totalLength += Encoding.UTF8.GetString(dataChunks[idx]).Length;
                }

                return dataChunks;
            }
            return new List<byte[]> { Encoding.UTF8.GetBytes("") };
        }
    }
}

namespace Venomcc.Utility.Command
{
    public static class CommandUtilities
    {
        private static Dictionary<string, ICommand.ICommand> commands = new Dictionary<string, ICommand.ICommand>()
        {
            ["listCons"] = new listCons(),
        };

        static readonly public CommandInterpreter commandInterpreter = new CommandInterpreter();

        public static ICommand.ICommand? getCommand(string commandName)
        {
            if (isCommandExist(commandName))
            {
                return commands[commandName];
            }
            return null;
        }

        public static bool isCommandExist(string commandName)
        {
            bool commandExists = commands.ContainsKey(commandName) ? true : false;
            return commandExists;
        }
    }
}
