// YAML 결합부 (packages/sim/src/data.ts 이관 — ADR-029).
//
// 로더는 두 층으로 나뉜다:
//   ① EffectDef · EnemyEffectDef  — YAML 키와 1:1 로 붙는 타입 (Types.cs 상단 주석의 못).
//      snake_case ↔ PascalCase 는 YamlDotNet 의 UnderscoredNamingConvention 규칙을 그대로 쓰되,
//      **모르는 키를 조용히 버리지 않도록** 여기 전용 변환기가 직접 매핑을 읽는다.
//      · EffectDef      : 모르는 키 = 예외. 카드 YAML 에 새 키가 생겼는데 필드를 안 만들면
//                         값이 사라지는 대신 로드가 실패한다.
//      · EnemyEffectDef : 모르는 키 = Extra 로 수집 (TS 의 인덱스 시그니처 `[k: string]: unknown`
//                         대응 — rebut_debuff 의 priority·counter_rebut 등이 여기로 간다).
//   ② 그 밖의 타입 — 로더가 필드별로 명시 매핑한다 (TS 로더와 같은 방식). 원시 DTO 는 Raw*.
//
// duration 은 `number | 'combat'` 유니온이라 스칼라 하나를 DurationSpec 으로 갈라 담는다
// (숫자면 Turns, 아니면 Keyword) — 기본 스칼라 매핑에 맡기면 3 이 문자열 "3" 이 되어
// equipment_dot 지속 턴이 조용히 기본값으로 떨어진다.

using System.Globalization;
using System.Reflection;
using ReviewHero.Engine;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReviewHero.Data;

/// <summary>EffectDef · EnemyEffectDef 전용 매핑 변환기 (위 주석 ① 참조)</summary>
internal sealed class EffectMappingConverter : IYamlTypeConverter
{
    private static readonly INamingConvention Naming = UnderscoredNamingConvention.Instance;

    /// <summary>타입별 「YAML 키 → 속성」 표. 속성명을 snake_case 로 되돌려 만든다.</summary>
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> KeyMaps = new();

    private static Dictionary<string, PropertyInfo> KeyMap(Type t)
    {
        lock (KeyMaps)
        {
            if (KeyMaps.TryGetValue(t, out var map)) return map;
            map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name == nameof(EnemyEffectDef.Extra)) continue; // 수집 자리 자체는 키가 아니다
                map[Naming.Apply(p.Name)] = p;
            }
            KeyMaps[t] = map;
            return map;
        }
    }

    public bool Accepts(Type type) => type == typeof(EffectDef) || type == typeof(EnemyEffectDef);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        bool collectExtra = type == typeof(EnemyEffectDef);
        var map = KeyMap(type);
        var values = new Dictionary<PropertyInfo, object?>();
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);

        parser.Consume<MappingStart>();
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            string key = parser.Consume<Scalar>().Value;
            if (map.TryGetValue(key, out var prop))
            {
                values[prop] = ReadTyped(parser, prop.PropertyType, type, key);
            }
            else if (collectExtra)
            {
                extra[key] = ReadAny(parser);
            }
            else
            {
                throw new YamlException(
                    $"{type.Name}: 알 수 없는 키 '{key}' — 필드를 추가하거나 YAML 을 고칠 것 " +
                    $"(키: {string.Join(", ", map.Keys)})");
            }
        }

        object obj = Activator.CreateInstance(type)!;
        foreach (var (prop, value) in values) prop.SetValue(obj, value);
        if (collectExtra && extra.Count > 0)
        {
            typeof(EnemyEffectDef).GetProperty(nameof(EnemyEffectDef.Extra))!.SetValue(obj, extra);
        }
        return obj;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
        throw new NotSupportedException("로더는 읽기 전용이다");

    private static object? ReadTyped(IParser parser, Type target, Type owner, string key)
    {
        var t = Nullable.GetUnderlyingType(target) ?? target;

        if (t == typeof(DurationSpec))
        {
            string s = parser.Consume<Scalar>().Value;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                ? new DurationSpec { Turns = n }
                : new DurationSpec { Keyword = s };
        }

        string raw = parser.Consume<Scalar>().Value;
        if (t == typeof(string)) return raw;
        if (t == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
        if (t == typeof(double)) return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (t == typeof(bool)) return bool.Parse(raw);
        throw new YamlException($"{owner.Name}.{key}: 지원하지 않는 필드 타입 {target.Name}");
    }

    /// <summary>스키마 밖 키의 값 — 구조 그대로 담는다 (TS 가 객체를 통째로 흘려 담던 것과 같다)</summary>
    private static object? ReadAny(IParser parser)
    {
        if (parser.TryConsume<Scalar>(out var scalar)) return scalar.Value;

        if (parser.TryConsume<SequenceStart>(out _))
        {
            var list = new List<object?>();
            while (!parser.TryConsume<SequenceEnd>(out _)) list.Add(ReadAny(parser));
            return list;
        }

        if (parser.TryConsume<MappingStart>(out _))
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                string k = parser.Consume<Scalar>().Value;
                dict[k] = ReadAny(parser);
            }
            return dict;
        }

        parser.SkipThisAndNestedEvents();
        return null;
    }
}

internal static class Yaml
{
    /// <summary>
    /// 로더 전용 역직렬화기. 원시 DTO(Raw*)는 명시 매핑 전 단계라 문서용 키(role·text·fixed_review…)를
    /// 그대로 흘려보내야 하므로 IgnoreUnmatchedProperties 를 켠다. YAML 키와 1:1 인 두 타입만은
    /// <see cref="EffectMappingConverter"/> 가 가로채 엄격하게 읽는다.
    /// </summary>
    public static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new EffectMappingConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    public static T Load<T>(string path) => Deserializer.Deserialize<T>(File.ReadAllText(path));
}
