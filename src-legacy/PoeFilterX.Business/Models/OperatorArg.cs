using PoeFilterX.Business.Enums;

namespace PoeFilterX.Business.Models
{
    public class OperatorArg<T>
    {
        public OperatorArg(T value, FilterOperator filterOperator)
        {
            Value = value;
            Operator = filterOperator;
        }

        public FilterOperator Operator { get; set; }
        public T Value { get; set; }
    }
}