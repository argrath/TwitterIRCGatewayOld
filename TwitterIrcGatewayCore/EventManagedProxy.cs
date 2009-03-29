using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Text;
using System.Diagnostics;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class EventMangedProxy<T> : RealProxy where T : MarshalByRefObject
    {
        private static readonly Dictionary<MethodInfo, EventInfo> EventsByAddMethods = new Dictionary<MethodInfo, EventInfo>();
        private static readonly Dictionary<MethodInfo, EventInfo> EventsByRemoveMethods = new Dictionary<MethodInfo, EventInfo>();

        private T _targetObject;
        private Dictionary<EventInfo, List<Delegate>> _eventHandlers = new Dictionary<EventInfo, List<Delegate>>();

        static EventMangedProxy()
        {
            foreach (var ev in typeof(T).GetEvents())
            {
                EventsByAddMethods.Add(ev.GetAddMethod(), ev);
                EventsByRemoveMethods.Add(ev.GetRemoveMethod(), ev);
            }
        }

        public EventMangedProxy(T aObj)
            : base(typeof(T))
        {
            _targetObject = aObj;
        }

        public void RemoveAllEvents()
        {
            foreach (var evHandlers in _eventHandlers)
            {
                EventInfo evInfo = evHandlers.Key;
                foreach (var evHandler in evHandlers.Value)
                    evInfo.RemoveEventHandler(_targetObject, evHandler);
            }
        }

        [DebuggerStepThrough]
        public override IMessage Invoke(IMessage msg)
        {
            IMethodMessage methodMessage = msg as IMethodMessage;
            MethodInfo methodInfo = (MethodInfo)methodMessage.MethodBase;
            if (EventsByAddMethods.ContainsKey(methodInfo))
            {
                EventInfo eventInfo = EventsByAddMethods[methodInfo];
                if (!_eventHandlers.ContainsKey(eventInfo))
                    _eventHandlers[eventInfo] = new List<Delegate>();
                _eventHandlers[eventInfo].Add((Delegate)methodMessage.Args[0]);
            }
            else if (EventsByRemoveMethods.ContainsKey(methodInfo))
            {
                EventInfo eventInfo = EventsByRemoveMethods[methodInfo];
                if (_eventHandlers.ContainsKey(eventInfo))
                    _eventHandlers[eventInfo].Remove((Delegate)methodMessage.Args[0]);

            }
            return RemotingServices.ExecuteMessage(_targetObject, (IMethodCallMessage)msg);
        }
    }
}
