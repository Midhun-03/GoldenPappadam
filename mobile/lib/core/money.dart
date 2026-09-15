import 'package:intl/intl.dart';

final _rupees = NumberFormat.currency(locale: 'en_IN', symbol: '₹', decimalDigits: 2);
final _quantity = NumberFormat.decimalPattern('en_IN');
final _time = DateFormat('h:mm a');
final _dayAndTime = DateFormat('d MMM, h:mm a');

String money(double value) => _rupees.format(value);

String quantity(double value) => _quantity.format(value);

/// Times are shown in the phone's own zone, which on the route is IST anyway.
String timeOfDay(DateTime value) => _time.format(value.toLocal());

String dayAndTime(DateTime value) => _dayAndTime.format(value.toLocal());

/// "2 minutes ago" beats a timestamp when the question is "how stale is this balance?".
String howLongAgo(DateTime? value) {
  if (value == null) return 'never synced';

  final gap = DateTime.now().difference(value.toLocal());

  if (gap.inMinutes < 1) return 'just now';
  if (gap.inMinutes < 60) return '${gap.inMinutes} min ago';
  if (gap.inHours < 24) return '${gap.inHours} h ago';

  return '${gap.inDays} d ago';
}
