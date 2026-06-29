using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Heathen.Editor
{
    /// <summary>
    /// Newtonsoft converters for the common UnityEngine value types so settings POCOs can use them and
    /// round-trip to clean JSON. Vanilla Newtonsoft would otherwise emit Unity's computed get-only
    /// properties (e.g. <c>Color.grayscale</c>, <c>Vector3.normalized</c>), cluttering the file. Used by
    /// <see cref="SettingsStore"/>; public so tools can reuse the set for their own serialisation.
    /// </summary>
    public static class UnityJson
    {
        /// <summary>Converters for Color, Color32, Vector2, Vector3, Vector4 and Quaternion.</summary>
        public static JsonConverter[] Converters { get; } =
        {
            new ColorJson(), new Color32Json(),
            new Vector2Json(), new Vector3Json(), new Vector4Json(), new QuaternionJson(),
        };

        private static float F(JObject o, string k, float fallback = 0f) => o.Value<float?>(k) ?? fallback;
        private static byte  B(JObject o, string k, byte  fallback)      => (byte)(o.Value<int?>(k) ?? fallback);

        private sealed class ColorJson : JsonConverter<Color>
        {
            public override void WriteJson(JsonWriter w, Color v, JsonSerializer s)
            {
                w.WriteStartObject();
                w.WritePropertyName("r"); w.WriteValue(v.r);
                w.WritePropertyName("g"); w.WriteValue(v.g);
                w.WritePropertyName("b"); w.WriteValue(v.b);
                w.WritePropertyName("a"); w.WriteValue(v.a);
                w.WriteEndObject();
            }
            public override Color ReadJson(JsonReader r, Type t, Color e, bool h, JsonSerializer s)
            {
                var o = JObject.Load(r);
                return new Color(F(o, "r"), F(o, "g"), F(o, "b"), F(o, "a", 1f));
            }
        }

        private sealed class Color32Json : JsonConverter<Color32>
        {
            public override void WriteJson(JsonWriter w, Color32 v, JsonSerializer s)
            {
                w.WriteStartObject();
                w.WritePropertyName("r"); w.WriteValue(v.r);
                w.WritePropertyName("g"); w.WriteValue(v.g);
                w.WritePropertyName("b"); w.WriteValue(v.b);
                w.WritePropertyName("a"); w.WriteValue(v.a);
                w.WriteEndObject();
            }
            public override Color32 ReadJson(JsonReader r, Type t, Color32 e, bool h, JsonSerializer s)
            {
                var o = JObject.Load(r);
                return new Color32(B(o, "r", 0), B(o, "g", 0), B(o, "b", 0), B(o, "a", 255));
            }
        }

        private sealed class Vector2Json : JsonConverter<Vector2>
        {
            public override void WriteJson(JsonWriter w, Vector2 v, JsonSerializer s)
            {
                w.WriteStartObject();
                w.WritePropertyName("x"); w.WriteValue(v.x);
                w.WritePropertyName("y"); w.WriteValue(v.y);
                w.WriteEndObject();
            }
            public override Vector2 ReadJson(JsonReader r, Type t, Vector2 e, bool h, JsonSerializer s)
            {
                var o = JObject.Load(r);
                return new Vector2(F(o, "x"), F(o, "y"));
            }
        }

        private sealed class Vector3Json : JsonConverter<Vector3>
        {
            public override void WriteJson(JsonWriter w, Vector3 v, JsonSerializer s)
            {
                w.WriteStartObject();
                w.WritePropertyName("x"); w.WriteValue(v.x);
                w.WritePropertyName("y"); w.WriteValue(v.y);
                w.WritePropertyName("z"); w.WriteValue(v.z);
                w.WriteEndObject();
            }
            public override Vector3 ReadJson(JsonReader r, Type t, Vector3 e, bool h, JsonSerializer s)
            {
                var o = JObject.Load(r);
                return new Vector3(F(o, "x"), F(o, "y"), F(o, "z"));
            }
        }

        private sealed class Vector4Json : JsonConverter<Vector4>
        {
            public override void WriteJson(JsonWriter w, Vector4 v, JsonSerializer s)
            {
                w.WriteStartObject();
                w.WritePropertyName("x"); w.WriteValue(v.x);
                w.WritePropertyName("y"); w.WriteValue(v.y);
                w.WritePropertyName("z"); w.WriteValue(v.z);
                w.WritePropertyName("w"); w.WriteValue(v.w);
                w.WriteEndObject();
            }
            public override Vector4 ReadJson(JsonReader r, Type t, Vector4 e, bool h, JsonSerializer s)
            {
                var o = JObject.Load(r);
                return new Vector4(F(o, "x"), F(o, "y"), F(o, "z"), F(o, "w"));
            }
        }

        private sealed class QuaternionJson : JsonConverter<Quaternion>
        {
            public override void WriteJson(JsonWriter w, Quaternion v, JsonSerializer s)
            {
                w.WriteStartObject();
                w.WritePropertyName("x"); w.WriteValue(v.x);
                w.WritePropertyName("y"); w.WriteValue(v.y);
                w.WritePropertyName("z"); w.WriteValue(v.z);
                w.WritePropertyName("w"); w.WriteValue(v.w);
                w.WriteEndObject();
            }
            public override Quaternion ReadJson(JsonReader r, Type t, Quaternion e, bool h, JsonSerializer s)
            {
                var o = JObject.Load(r);
                return new Quaternion(F(o, "x"), F(o, "y"), F(o, "z"), F(o, "w"));
            }
        }
    }
}
