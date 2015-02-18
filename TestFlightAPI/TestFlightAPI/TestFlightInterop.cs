﻿using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace TestFlightAPI
{
    public class TestFlightInterop : PartModule, ITestFlightInterop
    {
        private Dictionary<String, InteropValue> knownInterops;

        public bool AddInteropValue(string name, string value, string owner)
        {
            name = name.ToLower().Trim();
            if (!RemoveInteropValue(name, owner))
            {
                Debug.Log("remove interop returned false");
                return false;
            }

            InteropValue opValue = new InteropValue();
            opValue.owner = owner;
            opValue.value = value;
            opValue.valueType = InteropValueType.STRING;

            knownInterops.Add(name, opValue);

            Debug.Log("Added new interop " + name + " = " + value + ", for " + owner);

            return true;
        }
        public bool AddInteropValue(string name, int value, string owner)
        {
            name = name.ToLower().Trim();
            if (!RemoveInteropValue(name, owner))
                return false;

            InteropValue opValue = new InteropValue();
            opValue.owner = owner;
            opValue.value = String.Format("{0:D}",value);
            opValue.valueType = InteropValueType.INT;

            knownInterops.Add(name, opValue);

            Debug.Log("Added new interop " + name + " = " + value + ", for " + owner);

            return true;
        }
        public bool AddInteropValue(string name, float value, string owner)
        {
            name = name.ToLower().Trim();
            if (!RemoveInteropValue(name, owner))
                return false;

            InteropValue opValue = new InteropValue();
            opValue.owner = owner;
            opValue.value = String.Format("{0:F4}",value);
            opValue.valueType = InteropValueType.FLOAT;

            knownInterops.Add(name, opValue);

            Debug.Log("Added new interop " + name + " = " + value + ", for " + owner);

            return true;
        }
        public bool AddInteropValue(string name, bool value, string owner)
        {
            name = name.ToLower().Trim();
            if (!RemoveInteropValue(name, owner))
                return false;

            InteropValue opValue = new InteropValue();
            opValue.owner = owner;
            opValue.value = String.Format("{0:D}",value);
            opValue.valueType = InteropValueType.BOOL;

            knownInterops.Add(name, opValue);

            Debug.Log("Added new interop " + name + " = " + value + ", for " + owner);

            return true;
        }
        public bool RemoveInteropValue(string name, string owner)
        {
            name = name.ToLower().Trim();
            if (knownInterops == null)
                knownInterops = new Dictionary<string, InteropValue>();

            if (!knownInterops.ContainsKey(name))
                return true;

            InteropValue opValue = knownInterops[name];
            if (opValue.owner != owner)
                return false;

            knownInterops.Remove(name);
            return true;
        }
        public void ClearInteropValues(string owner)
        {
            List<String> keysToDelete = new List<string>();

            foreach (string key in knownInterops.Keys)
            {
                if (knownInterops[key].owner == owner)
                    keysToDelete.Add(key);
            }

            if (keysToDelete.Count > 0)
            {
                foreach (string key in keysToDelete)
                {
                    knownInterops.Remove(key);
                }
            }
        }
        public InteropValue GetInterop(string name)
        {
            name = name.ToLower().Trim();
            if (knownInterops == null)
            {
                InteropValue returnVal = new InteropValue();
                returnVal.valueType = InteropValueType.INVALID;
                return returnVal;
            }

            if (knownInterops.ContainsKey(name))
                return knownInterops[name];
            else
            {
                InteropValue returnVal = new InteropValue();
                returnVal.valueType = InteropValueType.INVALID;
                return returnVal;
            }
        }
    }
}

