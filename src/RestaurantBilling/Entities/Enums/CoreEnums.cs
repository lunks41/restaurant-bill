namespace Entities.Enums;

public enum BillType { DineIn, Takeaway, Delivery, Aggregator, QuoteConverted }
public enum BillStatus { Draft, Paid, Partial, Cancelled, Refunded }
public enum PaymentMode { Cash, Card, UPI, Wallet, Credit, Mixed }
public enum TaxType { GST, StateVAT, Exempt }
public enum StockReferenceType { Opening, Purchase, Sale, Loss, Adjustment, Transfer, VoidReversal }
public enum NumberSeriesKey { Bill, Quote, KOT, Receipt, PO, GRN, Adjustment, Loss }

