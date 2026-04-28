using FluentValidation;

namespace Services.Billing.Commands.SettleBill;

public class SettleBillCommandValidator : AbstractValidator<SettleBillCommand>
{
    public SettleBillCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.Payments).NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ItemId).GreaterThan(0);
            item.RuleFor(x => x.ItemName).NotEmpty();
            item.RuleFor(x => x.Qty).GreaterThan(0);
            item.RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
            item.RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
            item.RuleFor(x => x.TaxPercent).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.Payments).ChildRules(pay =>
        {
            pay.RuleFor(x => x.Amount).GreaterThan(0);
        });
    }
}

