//
// Copyright (c) Antmicro
// Copyright (c) Realtime Embedded
//
// This file is part of the Emul8 project.
// Full license details are defined in the 'LICENSE' file.
//
using System;
using System.IO;
using Antmicro.Migrant;
using Antmicro.Migrant.Customization;
using Emul8.Core;
using Emul8.Peripherals.UART;
using System.Collections.Generic;
using Emul8.Logging;

namespace Emul8.EventRecording
{
    public class Player : IDisposable
    {
        public Player(FileStream stream, Machine machine)
        {
            this.machine = machine;
            this.stream = stream;
            deserializer = new Serializer(new Settings(useBuffering: false)).ObtainOpenStreamDeserializer(stream);
            handlersCache = new Dictionary<NameAndHandler, Delegate>();
            entries = deserializer.DeserializeMany<IRecordEntry>().GetEnumerator();
            if(!entries.MoveNext())
            {
                entries = null;
            }
        }

        public void Play(long syncNumber)
        {
            if(entries == null)
            {
                return;
            }
            while(entries.Current.SyncNumber <= syncNumber)
            {
                var entry = entries.Current;
                entry.Play(ResolveHandler);
                if(!entries.MoveNext())
                {
                    entries = null;
                    return;
                }
            }
        }
            
        public void Dispose()
        {
            stream.Close();
        }

        private Delegate ResolveHandler(string name, Delegate nullifiedHandler)
        {
            var nameAndHandler = new NameAndHandler(name, nullifiedHandler);
            if(handlersCache.ContainsKey(nameAndHandler))
            {
                return handlersCache[nameAndHandler];
            }
            var target = machine[name];
            var result = Delegate.CreateDelegate(nullifiedHandler.GetType(), target, nullifiedHandler.Method);
            handlersCache.Add(nameAndHandler, result);
            return result;
        }
            
        private IEnumerator<IRecordEntry> entries;
        private readonly Serializer.OpenStreamDeserializer deserializer;
        private readonly Dictionary<NameAndHandler, Delegate> handlersCache;
        private readonly FileStream stream;
        private readonly Machine machine;

        private struct NameAndHandler
        {
            public NameAndHandler(string name, Delegate handler) : this()
            {
                this.Name = name;
                this.Handler = handler;
            }           

            public string Name { get; private set; }
            public Delegate Handler { get; private set; }

            public override bool Equals(object obj)
            {
                if(obj == null)
                {
                    return false;
                }
                if(obj.GetType() != typeof(NameAndHandler))
                {
                    return false;
                }
                var other = (NameAndHandler)obj;
                return this == other;
            }            

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Name != null ? Name.GetHashCode() : 0) ^ (Handler != null ? Handler.GetHashCode() : 0);
                }
            }

            public static bool operator==(NameAndHandler first, NameAndHandler second)
            {
                return first.Name == second.Name && first.Handler == second.Handler;
            }

            public static bool operator!=(NameAndHandler first, NameAndHandler second)
            {
                return !(first == second);
            }
            
        }
    }
}
