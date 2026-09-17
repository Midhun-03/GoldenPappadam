import 'package:flutter/material.dart';

/// A dropdown you can type into to narrow it down. Used for the shop and for each product on a bill.
///
/// A thin wrapper over Material's [DropdownMenu], so the bill screen does not repeat the same
/// half-dozen settings for every field. Filtering matches anywhere in the label, ignoring case,
/// plus any extra text an entry supplies through [searchTextOf] - a shop can be found by its phone
/// number as well as its name.
class SearchableDropdown<T> extends StatelessWidget {
  const SearchableDropdown({
    required this.entries,
    required this.onSelected,
    this.selected,
    this.label,
    this.hintText,
    this.helperText,
    this.searchTextOf,
    super.key,
  });

  final List<DropdownMenuEntry<T>> entries;
  final ValueChanged<T?> onSelected;
  final T? selected;
  final String? label;
  final String? hintText;
  final String? helperText;
  final String Function(T value)? searchTextOf;

  @override
  Widget build(BuildContext context) => DropdownMenu<T>(
        dropdownMenuEntries: entries,
        initialSelection: selected,
        onSelected: onSelected,
        label: label == null ? null : Text(label!),
        hintText: hintText,
        helperText: helperText,
        expandedInsets: EdgeInsets.zero,
        menuHeight: 320,
        enableFilter: true,
        requestFocusOnTap: true,
        leadingIcon: const Icon(Icons.search),
        filterCallback: (entries, filter) {
          final needle = filter.trim().toLowerCase();
          if (needle.isEmpty) return entries;

          return entries
              .where((entry) =>
                  entry.label.toLowerCase().contains(needle) ||
                  (searchTextOf?.call(entry.value).toLowerCase().contains(needle) ?? false))
              .toList();
        },
      );
}
