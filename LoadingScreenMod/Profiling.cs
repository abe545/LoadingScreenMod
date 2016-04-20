using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Diagnostics;
using UnityEngine;

namespace LoadingScreenMod
{
    public static class Profiling
    {
        internal static readonly Stopwatch stopWatch = new Stopwatch();
        static FastList<LoadingProfiler.Event> customAssetEvents;
        internal const string NOT_FOUND = " (not found)", FAILED = " (failed)";

        internal static void Init()
        {
            Sink.builder.Length = 0;
            customAssetEvents = ProfilerSource.GetEvents(LoadingManager.instance.m_loadingProfilerCustomAsset);
            stopWatch.Reset();
        }

        internal static void Stop()
        {
            Sink.builder.Length = 0; Sink.builder.Capacity = 0;
            customAssetEvents = null;
            stopWatch.Reset();
        }

        internal static int Millis => (int) stopWatch.ElapsedMilliseconds;
        internal static long Ticks => stopWatch.ElapsedTicks;
        internal static void CustomAssetFailed(string name) => ModifyEvent(customAssetEvents, name, FAILED);
        internal static void CustomAssetNotFound(string name) => ModifyEvent(customAssetEvents, name, NOT_FOUND);

        static void ModifyEvent(FastList<LoadingProfiler.Event> events, string eventName, string postfix)
        {
            try
            {
                LoadingProfiler.Event[] buffer = events.m_buffer;

                if (!string.IsNullOrEmpty(eventName))
                    for (int i = events.m_size - 1, k = 5; i >= 0 && k >= 0; i--, k--)
                        if (!string.IsNullOrEmpty(buffer[i].m_name) && eventName == buffer[i].m_name)
                            buffer[i].m_name = string.Concat(eventName, postfix);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        /// <summary>
        /// Returns true if the simulation thread is now paused (between Deserialize and AfterDeserialize).
        /// </summary>
        //internal static bool SimulationPaused()
        //{
        //    try
        //    {
        //        FastList<LoadingProfiler.Event> events = ProfilerSource.GetEvents(LoadingManager.instance.m_loadingProfilerSimulation);
        //        int i = events.m_size - 1;
        //        return i > 4 && events.m_buffer[i].m_type == LoadingProfiler.Type.PauseLoading;
        //    }
        //    catch (Exception e)
        //    {
        //        UnityEngine.Debug.LogException(e);
        //        return true;
        //    }
        //}
    }

    sealed class Sink
    {
        string name, last;
        readonly Queue<string> queue = new Queue<string>();
        readonly int len;
        internal string Name { set { name = value; } }
        internal string Last => last;
        string NameLoading => string.Concat(YELLOW, name, OFF);
        string NameIdle => string.Concat(GRAY, name, OFF);
        string NameFailed => string.Concat(RED, name, Profiling.FAILED, OFF);

        internal const string YELLOW = "<color #f0e000>", RED = "<color #f80000>", GRAY = "<color #c0c0c0>", OFF = "</color>";
        internal static readonly StringBuilder builder = new StringBuilder();

        internal Sink(string name, int len)
        {
            this.name = name;
            this.len = len;
        }

        internal void Clear()
        {
            queue.Clear();
            last = null;
        }

        internal void Add(string s)
        {
            if (s != last)
            {
                if (last != null && len > 1)
                {
                    if (queue.Count >= len - 1)
                        queue.Dequeue();

                    queue.Enqueue(last);
                }

                if (s[s.Length - 1] == ')' && (s.EndsWith(Profiling.NOT_FOUND) || s.EndsWith(Profiling.FAILED)))
                    s = string.Concat(RED, s, OFF);

                last = s;
            }
        }

        internal string CreateText(bool isLoading, bool failed = false)
        {
            builder.AppendLine(isLoading ? NameLoading : failed ? NameFailed : NameIdle);

            foreach (string s in queue)
                builder.AppendLine(s);

            if (last != null)
                builder.Append(last);

            string ret = builder.ToString();
            builder.Length = 0;
            return ret;
        }
    }

    internal abstract class Source
    {
        protected internal abstract string CreateText();
    }

    internal class ProfilerSource : Source
    {
        protected readonly Sink sink;
        protected readonly FastList<LoadingProfiler.Event> events;
        protected int index;
        readonly bool alwaysLoading;

        static readonly FieldInfo eventsField = typeof(LoadingProfiler).GetField("m_events", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static FastList<LoadingProfiler.Event> GetEvents(LoadingProfiler profiler) => (FastList<LoadingProfiler.Event>) eventsField.GetValue(profiler);

        protected bool IsLoading
        {
            get
            {
                if (alwaysLoading)
                    return true;

                int lst = events.m_size - 1;
                LoadingProfiler.Event[] buffer = events.m_buffer;
                return lst >= 0 && ((int) buffer[lst].m_type & 1) == 0; // true if the last one is begin or continue
            }
        }

        internal ProfilerSource(string name, int len, LoadingProfiler profiler) : this(profiler, new Sink(name, len)) { }

        internal ProfilerSource(LoadingProfiler profiler, Sink sink, bool alwaysLoading = false) : base()
        {
            this.sink = sink;
            this.events = GetEvents(profiler);
            this.alwaysLoading = alwaysLoading;
        }

        protected internal override string CreateText()
        {
            try
            {
                int i = index, len = events.m_size;

                if (i >= len)
                    return null;

                index = len;
                LoadingProfiler.Event[] buffer = events.m_buffer;

                for (; i < len; i++)
                    switch (buffer[i].m_type)
                    {
                        case LoadingProfiler.Type.BeginLoading:
                        case LoadingProfiler.Type.BeginSerialize:
                        case LoadingProfiler.Type.BeginDeserialize:
                        case LoadingProfiler.Type.BeginAfterDeserialize:
                            sink.Add(buffer[i].m_name);
                            break;
                    }

                return sink.CreateText(IsLoading);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return null;
            }
        }
    }

    internal sealed class SimpleProfilerSource : ProfilerSource
    {
        bool failed;

        internal SimpleProfilerSource(string name, LoadingProfiler profiler) : base(name, 1, profiler) { }

        protected internal override string CreateText()
        {
            try
            {
                if (failed)
                    return sink.CreateText(false, true);

                LoadingProfiler.Event[] buffer = events.m_buffer;

                for (int i = events.m_size - 1; i >= 0; i--)
                    switch (buffer[i].m_type)
                    {
                        case LoadingProfiler.Type.BeginLoading:
                        case LoadingProfiler.Type.BeginSerialize:
                        case LoadingProfiler.Type.BeginDeserialize:
                        case LoadingProfiler.Type.BeginAfterDeserialize:
                            if (i != index || IsLoading)
                            {
                                index = i;
                                sink.Add(buffer[i].m_name);
                                return sink.CreateText(true);
                            }
                            else
                            {
                                sink.Clear();
                                return sink.CreateText(false);
                            }
                    }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }

            return null;
        }

        internal void Failed(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                message = sink.Last;

                if (string.IsNullOrEmpty(message))
                    message = "Deserialize";
            }
            else if (message.Length > 70)
                message = message.Substring(0, 70);

            message = string.Concat(Sink.RED, message, Sink.OFF);
            sink.Clear();
            sink.Add(message);
            failed = true;
        }
    }

    internal sealed class DualProfilerSource : Source
    {
        readonly Source scenes, assets;
        readonly Sink sink;
        readonly string name;
        int state = 0;
        int failed, notFound;

        internal DualProfilerSource(string name, int len) : base()
        {
            this.sink = new Sink(name, len);
            this.name = name;
            this.scenes = new ProfilerSource(LoadingManager.instance.m_loadingProfilerScenes, sink);
            this.assets = new ProfilerSource(LoadingManager.instance.m_loadingProfilerCustomAsset, sink, true);
        }

        protected internal override string CreateText()
        {
            string ret = state == 1 ? assets.CreateText() : scenes.CreateText();

            if (state == 0 && AssetLoader.instance.hasStarted)
                state = 1;
            else if (state == 1 && AssetLoader.instance.hasFinished)
                state = 2;

            return ret;
        }

        internal void SomeFailed() { failed++; AdjustName(); }
        internal void SomeNotFound() { notFound++; AdjustName(); }

        void AdjustName()
        {
            string s1 = failed == 0 ? String.Empty : string.Concat(failed.ToString(), " failed ");
            string s2 = notFound == 0 ? String.Empty : string.Concat(notFound.ToString(), " not found");
            sink.Name = name + " (" + s1 + s2 + ")";
        }
    }

    internal sealed class TimeSource : Source
    {
        protected internal override string CreateText()
        {
            int seconds = Profiling.Millis / 1000;
            return string.Concat((seconds / 60).ToString(), ":", (seconds % 60).ToString("00"));
        }
    }

    internal sealed class MemorySource : Source
    {
        int systemMegas = SystemInfo.systemMemorySize;

        protected internal override string CreateText()
        {
            try
            {
                ulong pagefileUsage, workingSetSize;
                MemoryAPI.GetUsage(out pagefileUsage, out workingSetSize);
                int pfMegas = (int) (pagefileUsage >> 20), wsMegas = (int) (workingSetSize >> 20);
                string gigas = (wsMegas / 1024f).ToString("F2");
                return pfMegas < systemMegas ? string.Concat(gigas, " GB") : string.Concat(Sink.RED, gigas, Sink.OFF, " GB");
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
