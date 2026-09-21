/// Page size for the newest-first log lists (audit log, stock movements). Matches the
/// server's default; a page shorter than this means there is nothing older to load.
const int kLogPageSize = 200;
