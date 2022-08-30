using System.Reflection;

namespace FileFlows.Server.Helpers;

using NPoco;

/// <summary>
/// Custom DB Mapper
/// </summary>
class CustomDbMapper : DefaultMapper
{
    /// <summary>
    /// Function to convert a database object type
    /// </summary>
    /// <param name="destType">the type of object to create</param>
    /// <param name="sourceType">the type of object to create a new object from</param>
    /// <returns>a function to do a conversion</returns>
    public override Func<object, object> GetFromDbConverter(Type destType, Type sourceType)
    {
        if (destType == typeof(Guid?) && sourceType == typeof(string))
            return (value) => string.IsNullOrEmpty(value as string) ? null : Guid.Parse((string)value);
        if (destType == typeof(Guid) && sourceType == typeof(string))
            return (value) => string.IsNullOrEmpty(value as string) ? Guid.Empty : Guid.Parse((string)value);
        if (destType == typeof(Dictionary<string, object>) && sourceType == typeof(string))
            return (value) =>
            {
                if (string.IsNullOrEmpty(value as string))
                    return new Dictionary<string, object>();
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>((string)value);
                return result;
            };
        return base.GetFromDbConverter(destType, sourceType);
    }
    
    public override Func<object, object> GetToDbConverter(Type destType, MemberInfo sourceMemberInfo)
    {
        if (sourceMemberInfo.GetMemberInfoType() == typeof(Guid))
            return (value) => value?.ToString() ?? string.Empty;
        if (sourceMemberInfo.GetMemberInfoType() == typeof(Guid?))
            return (value) => value?.ToString() ?? string.Empty;
        if (sourceMemberInfo.GetMemberInfoType() == typeof(string))
            return (value) => value?.ToString() ?? string.Empty;
        if (sourceMemberInfo.GetMemberInfoType() == typeof(Dictionary<string, object>))
            return (value) =>
            {
                if (value is Dictionary<string, object> dict == false || dict.Any() == false)
                    return string.Empty;
                return JsonSerializer.Serialize(dict);
            };
        return base.GetToDbConverter(destType, sourceMemberInfo);
    }
}