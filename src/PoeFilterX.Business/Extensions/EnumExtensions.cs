using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace PoeFilterX.Business.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDisplayName<TEnum>(this TEnum enumValue)
            where TEnum : Enum
     
        {
            var type = enumValue.GetType();
            var members = type.GetMember(enumValue.ToString());
            var attribute = members.FirstOrDefault(m => m.MemberType == MemberTypes.Field)?
                .GetCustomAttribute<DisplayAttribute>();

            return attribute?.GetName() ??
                enumValue.ToString();
        }
    }
}
