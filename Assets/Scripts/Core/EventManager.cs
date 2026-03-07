using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackAttack.Core
{
    public static class EventManager
    {
        private static Dictionary<string, Action<object>> eventDictionary = new Dictionary<string, Action<object>>();

        public static void StartListening(string eventName, Action<object> listener)
        {
            if (listener == null) return;

            if (eventDictionary.TryGetValue(eventName, out Action<object> thisEvent))
            {
                thisEvent += listener;
                eventDictionary[eventName] = thisEvent;
            }
            else
            {
                eventDictionary.Add(eventName, listener);
            }
        }

        public static void StopListening(string eventName, Action<object> listener)
        {
            if (listener == null) return;

            if (eventDictionary.TryGetValue(eventName, out Action<object> thisEvent))
            {
                thisEvent -= listener;
                
                if (thisEvent == null || thisEvent.GetInvocationList().Length == 0)
                {
                    eventDictionary.Remove(eventName);
                }
                else
                {
                    eventDictionary[eventName] = thisEvent;
                }
            }
        }

        public static void TriggerEvent(string eventName, object eventParam = null)
        {
            if (eventDictionary.TryGetValue(eventName, out Action<object> thisEvent))
            {
                thisEvent?.Invoke(eventParam);
            }
        }

        public static void ClearAllEvents()
        {
            eventDictionary.Clear();
        }
    }
}
