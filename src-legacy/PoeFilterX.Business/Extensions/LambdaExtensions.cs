using System.Linq.Expressions;
using System.Reflection;

namespace PoeFilterX.Business.Extensions
{
    public static class LambdaExtensions
    {
        /// <summary>
        /// Sets a Property's value through a lambda Expression Selector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="target"></param>
        /// <param name="memberLamda"></param>
        /// <param name="value"></param>
        /// <remarks>https://stackoverflow.com/a/9601914/7839602</remarks>
        public static void SetPropertyValue<T, TValue>(this T target, Expression<Func<T, TValue>> memberLamda, TValue value)
        {
            var memberSelectorExpression = memberLamda.Body as MemberExpression;
            var property = memberSelectorExpression?.Member as PropertyInfo;
            property?.SetValue(target, value, null);
        }

        /// <summary>
        /// Gets a Property Expression's name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="memberLamda"></param>
        /// <remarks>https://stackoverflow.com/a/9601914/7839602</remarks>
        public static string? GetName<T, TValue>(this Expression<Func<T, TValue>> memberLamda)
        {
            var memberSelectorExpression = memberLamda.Body as MemberExpression;
            var property = memberSelectorExpression?.Member as PropertyInfo;
            return property?.Name;
        }
    }
}
