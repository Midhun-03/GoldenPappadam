import 'package:flutter_test/flutter_test.dart';
import 'package:golden_pappadam_sales/features/sale/bill_draft.dart';

/// The rules of a bill being written. The one that matters is that picking the same packet twice
/// changes the line already there: a second line for the same product is a bill nobody meant to
/// write, and the server would refuse it anyway.
void main() {
  late BillDraft draft;

  setUp(() => draft = BillDraft());

  void addPacket({double quantity = 1}) => draft.add(
        productId: 'p1',
        productName: '20 piece packet',
        unitPrice: 35,
        quantity: quantity,
      );

  test('a new bill starts empty and totals nothing', () {
    expect(draft.isEmpty, isTrue);
    expect(draft.total, 0);
  });

  test('adding a product puts one line on the bill', () {
    addPacket(quantity: 10);

    expect(draft.lines, hasLength(1));
    expect(draft.lines.single.productName, '20 piece packet');
    expect(draft.total, 350);
  });

  test('picking the same product again adds to the line rather than making a second', () {
    addPacket(quantity: 10);
    addPacket(quantity: 5);

    expect(draft.lines, hasLength(1));
    expect(draft.quantityOf('p1'), 15);
    expect(draft.total, 525);
  });

  test('several different products each get their own line', () {
    addPacket(quantity: 10);
    draft.add(productId: 'p2', productName: '6 piece packet', unitPrice: 15, quantity: 4);

    expect(draft.lines, hasLength(2));
    expect(draft.total, 350 + 60);
  });

  test('a quantity can be set outright, and the total follows', () {
    addPacket(quantity: 10);

    draft.setQuantity('p1', 3);

    expect(draft.quantityOf('p1'), 3);
    expect(draft.total, 105);
  });

  test('stepping up and down changes the line', () {
    addPacket(quantity: 2);

    draft.changeQuantity('p1', 1);
    expect(draft.quantityOf('p1'), 3);

    draft.changeQuantity('p1', -2);
    expect(draft.quantityOf('p1'), 1);
  });

  test('stepping below one takes the line off, because a line for nothing means nothing', () {
    addPacket(quantity: 1);

    draft.changeQuantity('p1', -1);

    expect(draft.isEmpty, isTrue);
    expect(draft.contains('p1'), isFalse);
  });

  test('a line can be removed outright', () {
    addPacket(quantity: 10);
    draft.add(productId: 'p2', productName: '6 piece packet', unitPrice: 15, quantity: 4);

    draft.remove('p1');

    expect(draft.lines, hasLength(1));
    expect(draft.lines.single.productId, 'p2');
  });

  test('adding nothing is not a line', () {
    addPacket(quantity: 0);

    expect(draft.isEmpty, isTrue);
  });

  test('the price comes from the caller and the draft never invents one', () {
    // The shop's agreed price, handed in from the synced price list. Nothing here derives it.
    draft.add(productId: 'p1', productName: '20 piece packet', unitPrice: 34.5, quantity: 2);

    expect(draft.lines.single.unitPrice, 34.5);
    expect(draft.total, 69);
  });

  test('the lines handed out cannot be changed behind the draft back', () {
    addPacket();

    expect(() => draft.lines.clear(), throwsUnsupportedError);
  });
}
