using ErrorService.Shared;

namespace ErrorService.Client.Utils;

public static class OrderDisplayHelper
{
    public static OrderStage? EffectiveStage(OrderStatus status, OrderStage? stage)
    {
        if (status is OrderStatus.Rejected or OrderStatus.Cancelled
            or OrderStatus.PendingPayment or OrderStatus.ReceiptUploaded)
            return null;

        if (stage.HasValue) return stage.Value;
        return status == OrderStatus.Completed ? OrderStage.Resolved : OrderStage.PaymentConfirmed;
    }

    public static string Label(OrderStatus status, OrderStage? stage) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار پرداخت",
        OrderStatus.ReceiptUploaded => "در انتظار تایید",
        OrderStatus.Rejected => "رد شده",
        OrderStatus.Cancelled => "لغو شده",
        OrderStatus.Approved or OrderStatus.Completed =>
            OrderStageInfo.Label(EffectiveStage(status, stage) ?? OrderStage.PaymentConfirmed),
        _ => "نامشخص"
    };

    public static bool IsReturned(OrderDto order) =>
        order.ReturnCount > 0 || (order.Returns?.Count ?? 0) > 0;

    public static string Label(OrderDto order)
    {
        if (IsReturned(order) && order.Status is OrderStatus.Approved or OrderStatus.Completed)
            return "مرجوعی";
        return Label(order.Status, order.Stage);
    }

    public static string Color(OrderStatus status, OrderStage? stage) => status switch
    {
        OrderStatus.PendingPayment => "#f59e0b",
        OrderStatus.ReceiptUploaded => "#0891b2",
        OrderStatus.Rejected => "#dc2626",
        OrderStatus.Cancelled => "#64748b",
        OrderStatus.Approved or OrderStatus.Completed =>
            OrderStageInfo.Color(EffectiveStage(status, stage) ?? OrderStage.PaymentConfirmed),
        _ => "#64748b"
    };

    public static string Color(OrderDto order)
    {
        if (IsReturned(order) && order.Status is OrderStatus.Approved or OrderStatus.Completed)
            return "#dc2626";
        return Color(order.Status, order.Stage);
    }

    private static string Soft(string c) => $"background:{c}1a;color:{c};border:1px solid {c}55;";

    private static string Solid(string c) => $"background:{c};color:#fff;";

    public static string SoftBadgeStyle(OrderStatus status, OrderStage? stage) => Soft(Color(status, stage));

    public static string SoftBadgeStyle(OrderDto order) => Soft(Color(order));

    public static string SolidBadgeStyle(OrderStatus status, OrderStage? stage) => Solid(Color(status, stage));

    public static string SolidBadgeStyle(OrderDto order) => Solid(Color(order));
}
