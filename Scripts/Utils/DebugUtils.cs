using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    public static class DebugUtils
    {
        private const string InfoColor = "#CECBFF";
        private const string WarnColor = "#FFC925";
        private const string ErrorColor = "#FFC0C8";
        private const string SuccessColor = "#4FFF69";
        
        public static string Bold(this string str) => "<b>" + str + "</b>";
        public static string Italic(this string str) => "<i>" + str + "</i>";
        public static string Color(this string str, string color) => $"<color={color}>{str}</color>";
        
        public static void Log(string message, Object context)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={InfoColor}>[INFO: {context.name}] </color>".Bold() + $"{message}", context);
#endif
        }
        
        public static void LogList<T>(string listname, List<T> list, Object context) where T : Object
        {
#if UNITY_EDITOR || DEBUG
            string text = "";
            
            foreach (var obj in list)
            {
                text += obj.name;
            }
            
            Debug.Log($"<color={InfoColor}>[{listname} LIST: {context.name}] </color>".Bold() + $"{text}", context);
#endif
        }
        
        public static void LogList<T>(string listname, List<T> list) where T : Object
        {
#if UNITY_EDITOR || DEBUG
            string text = "";
            
            foreach (var obj in list)
            {
                text += obj.name;
            }
            
            Debug.Log($"<color={InfoColor}>[{listname} LIST] </color>".Bold() + $"{text}");
#endif
        }

        public static void Log(string message)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={InfoColor}>[INFO] </color>".Bold() + $"{message}");
#endif
        }

        public static void Success(string message, Object context)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={SuccessColor}>[OK: {context.name}] </color>".Bold() + $"{message}", context);
#endif
        }

        public static void Success(string message)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={SuccessColor}>[OK] </color>".Bold() + $"{message}");
#endif
        }

        public static void Warn(string message, Object context)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={WarnColor}>[WARN: {context.name}] </color>".Bold() + $"{message}", context);
#endif
        }

        public static void Warn(string message)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={WarnColor}>[WARN] </color>".Bold() + $"{message}");
#endif
        }

        public static void Error(string message, Object context)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={ErrorColor}>[ERROR: {context.name}] </color>".Bold() + $"{message}", context);
#endif
        }

        public static void Error(string message)
        {
#if UNITY_EDITOR || DEBUG
            Debug.Log($"<color={ErrorColor}>[ERROR] </color>".Bold() + $"{message}");
#endif
        }
    }
}