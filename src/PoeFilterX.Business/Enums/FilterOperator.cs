using System.ComponentModel.DataAnnotations;

namespace PoeFilterX.Business.Enums
{
    public enum FilterOperator
    {
        [Display(Name = "=")]
        Equals,
        [Display(Name = "!")]
        NotEquals,
        [Display(Name = ">")]
        GreaterThan,
        [Display(Name = "<")]
        LessThan,
        [Display(Name = "<=")]
        LessThanOrEqual,
        [Display(Name = ">=")]
        GreaterThanOrEqual,
        [Display(Name = "==")]
        ExactMatch
    }
}