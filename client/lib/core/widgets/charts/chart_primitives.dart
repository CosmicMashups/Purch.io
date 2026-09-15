import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../../theming/app_tokens.dart';
import 'chart_theme.dart';

/// One (label, value) pair. Every chart in this file consumes the same shape
/// so callers never have to learn a per-chart data model.
class ChartDatum {
  const ChartDatum({
    required this.label,
    required this.value,
    this.tooltipLabel,
    this.color,
  });

  final String label;
  final double value;

  /// Longer label for the tooltip when the axis label has to be abbreviated.
  final String? tooltipLabel;

  /// Overrides the palette slot — used where a color already carries meaning
  /// (low stock severity, movement type).
  final Color? color;

  String get resolvedTooltipLabel => tooltipLabel ?? label;
}

/// A named series of [ChartDatum]s sharing one color, for grouped charts.
class ChartSeries {
  const ChartSeries({
    required this.name,
    required this.color,
    required this.values,
    this.formatValue,
  });

  final String name;
  final Color color;
  final List<double> values;
  final String Function(double value)? formatValue;

  String format(double value) =>
      formatValue?.call(value) ?? ChartTheme.compactCount(value);
}

double _niceMax(double rawMax) {
  if (rawMax <= 0) {
    return 1;
  }
  return rawMax * 1.15;
}

Widget _axisText(String text, {TextAlign align = TextAlign.center}) {
  return Padding(
    padding: const EdgeInsets.only(top: 6),
    child: Text(
      text,
      textAlign: align,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: ChartTheme.axisLabel,
    ),
  );
}

FlGridData _grid(double interval) => FlGridData(
  show: true,
  drawVerticalLine: false,
  horizontalInterval: interval <= 0 ? 1 : interval,
  getDrawingHorizontalLine:
      (value) => const FlLine(color: ChartTheme.gridLine, strokeWidth: 1),
);

/// Revenue-over-time line chart — the Home page's primary trend surface.
class TrendLineChart extends StatelessWidget {
  const TrendLineChart({
    super.key,
    required this.data,
    this.formatValue = ChartTheme.peso,
    this.formatAxisValue = ChartTheme.compactPeso,
    this.color = ChartTheme.primarySeries,
  });

  final List<ChartDatum> data;
  final String Function(double) formatValue;
  final String Function(double) formatAxisValue;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final maxValue = _niceMax(
      data.fold<double>(0, (max, d) => d.value > max ? d.value : max),
    );
    final gridInterval = maxValue / 4;

    // Thin the x labels so a 365-point year series doesn't render 365 dates.
    final labelStride = (data.length / 7).ceil().clamp(1, data.length);

    return LineChart(
      LineChartData(
        minY: 0,
        maxY: maxValue,
        gridData: _grid(gridInterval),
        borderData: FlBorderData(show: false),
        titlesData: FlTitlesData(
          topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
          rightTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          leftTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 46,
              interval: gridInterval <= 0 ? 1 : gridInterval,
              getTitlesWidget:
                  (value, meta) => Padding(
                    padding: const EdgeInsets.only(right: 6),
                    child: Text(
                      formatAxisValue(value),
                      textAlign: TextAlign.right,
                      style: ChartTheme.axisLabel,
                    ),
                  ),
            ),
          ),
          bottomTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 26,
              interval: 1,
              getTitlesWidget: (value, meta) {
                final index = value.round();
                if (index < 0 || index >= data.length) {
                  return const SizedBox.shrink();
                }
                if (index % labelStride != 0 && index != data.length - 1) {
                  return const SizedBox.shrink();
                }
                return _axisText(data[index].label);
              },
            ),
          ),
        ),
        lineTouchData: LineTouchData(
          touchTooltipData: LineTouchTooltipData(
            getTooltipColor: (_) => ChartTheme.tooltipBackground,
            getTooltipItems:
                (spots) =>
                    spots.map((spot) {
                      final index = spot.x.round();
                      final label =
                          index >= 0 && index < data.length
                              ? data[index].resolvedTooltipLabel
                              : '';
                      return LineTooltipItem(
                        '$label\n${formatValue(spot.y)}',
                        ChartTheme.tooltipText,
                      );
                    }).toList(),
          ),
        ),
        lineBarsData: [
          LineChartBarData(
            spots: [
              for (var i = 0; i < data.length; i++)
                FlSpot(i.toDouble(), data[i].value),
            ],
            isCurved: true,
            curveSmoothness: 0.22,
            preventCurveOverShooting: true,
            color: color,
            barWidth: 2.5,
            dotData: FlDotData(show: data.length <= 14),
            belowBarData: BarAreaData(
              show: true,
              color: color.withValues(alpha: 0.10),
            ),
          ),
        ],
      ),
    );
  }
}

/// Grouped/stacked vertical bars — movement summary, sales per cashier,
/// shift attendance, category sales.
class GroupedBarChart extends StatelessWidget {
  const GroupedBarChart({
    super.key,
    required this.labels,
    required this.series,
    this.formatAxisValue = ChartTheme.compactCount,
    this.maxLabelLines = 1,
    this.colorsPerGroup,
  });

  /// One label per group (x position).
  final List<String> labels;

  /// One or more series; each must have `labels.length` values.
  final List<ChartSeries> series;

  final String Function(double) formatAxisValue;
  final int maxLabelLines;

  /// Per-group bar colors, for single-series charts where the *category*
  /// carries meaning (movement reason, stock severity) rather than the
  /// series. Ignored when there's more than one series — there, color has to
  /// mean "which series".
  final List<Color>? colorsPerGroup;

  @override
  Widget build(BuildContext context) {
    var rawMax = 0.0;
    for (final s in series) {
      for (final value in s.values) {
        if (value > rawMax) rawMax = value;
      }
    }
    final maxValue = _niceMax(rawMax);
    final gridInterval = maxValue / 4;
    final barWidth = series.length > 1 ? 9.0 : 16.0;

    return BarChart(
      BarChartData(
        alignment: BarChartAlignment.spaceAround,
        minY: 0,
        maxY: maxValue,
        gridData: _grid(gridInterval),
        borderData: FlBorderData(show: false),
        titlesData: FlTitlesData(
          topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
          rightTitles: const AxisTitles(
            sideTitles: SideTitles(showTitles: false),
          ),
          leftTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: 46,
              interval: gridInterval <= 0 ? 1 : gridInterval,
              getTitlesWidget:
                  (value, meta) => Padding(
                    padding: const EdgeInsets.only(right: 6),
                    child: Text(
                      formatAxisValue(value),
                      textAlign: TextAlign.right,
                      style: ChartTheme.axisLabel,
                    ),
                  ),
            ),
          ),
          bottomTitles: AxisTitles(
            sideTitles: SideTitles(
              showTitles: true,
              reservedSize: maxLabelLines > 1 ? 40 : 28,
              interval: 1,
              getTitlesWidget: (value, meta) {
                final index = value.round();
                if (index < 0 || index >= labels.length) {
                  return const SizedBox.shrink();
                }
                return Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Text(
                    labels[index],
                    textAlign: TextAlign.center,
                    maxLines: maxLabelLines,
                    overflow: TextOverflow.ellipsis,
                    style: ChartTheme.axisLabel,
                  ),
                );
              },
            ),
          ),
        ),
        barTouchData: BarTouchData(
          touchTooltipData: BarTouchTooltipData(
            getTooltipColor: (_) => ChartTheme.tooltipBackground,
            getTooltipItem: (group, groupIndex, rod, rodIndex) {
              final s = series[rodIndex];
              final label =
                  groupIndex >= 0 && groupIndex < labels.length
                      ? labels[groupIndex]
                      : '';
              return BarTooltipItem(
                '$label\n${s.name}: ${s.format(rod.toY)}',
                ChartTheme.tooltipText,
              );
            },
          ),
        ),
        barGroups: [
          for (var i = 0; i < labels.length; i++)
            BarChartGroupData(
              x: i,
              barsSpace: 4,
              barRods: [
                for (final s in series)
                  BarChartRodData(
                    toY: i < s.values.length ? s.values[i] : 0,
                    color:
                        series.length == 1 &&
                                colorsPerGroup != null &&
                                i < colorsPerGroup!.length
                            ? colorsPerGroup![i]
                            : s.color,
                    width: barWidth,
                    borderRadius: const BorderRadius.vertical(
                      top: Radius.circular(4),
                    ),
                  ),
              ],
            ),
        ],
      ),
    );
  }
}

/// Ranked horizontal bars. Built from layout primitives rather than fl_chart
/// because a ranked list needs its full item name readable next to the bar —
/// a rotated bar chart's axis labels get truncated to uselessness on a
/// tablet. Still uses the shared palette, radius and label styles.
class RankedBarList extends StatelessWidget {
  const RankedBarList({
    super.key,
    required this.data,
    required this.formatValue,
    this.trailingBuilder,
    this.barColor = ChartTheme.primarySeries,
  });

  final List<ChartDatum> data;
  final String Function(double) formatValue;

  /// Optional extra per-row annotation (e.g. "12 / alert at 20").
  final String? Function(ChartDatum datum)? trailingBuilder;

  final Color barColor;

  @override
  Widget build(BuildContext context) {
    final maxValue = data.fold<double>(
      0,
      (max, d) => d.value > max ? d.value : max,
    );

    return ListView.separated(
      padding: EdgeInsets.zero,
      itemCount: data.length,
      separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
      itemBuilder: (context, index) {
        final datum = data[index];
        final fraction = maxValue <= 0 ? 0.0 : (datum.value / maxValue);
        final annotation = trailingBuilder?.call(datum);

        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    datum.label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: ChartTheme.valueLabel.copyWith(
                      color: AppColors.textPrimary,
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                Text(
                  annotation ?? formatValue(datum.value),
                  style: ChartTheme.valueLabel,
                ),
              ],
            ),
            const SizedBox(height: 5),
            ClipRRect(
              borderRadius: BorderRadius.circular(AppRadius.sm),
              child: LinearProgressIndicator(
                value: fraction.clamp(0.0, 1.0),
                minHeight: 8,
                backgroundColor: AppColors.borderSubtle,
                valueColor: AlwaysStoppedAnimation<Color>(
                  datum.color ?? barColor,
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

/// Share-of-total donut — department and category breakdowns.
class ShareDonutChart extends StatefulWidget {
  const ShareDonutChart({
    super.key,
    required this.data,
    this.formatValue = ChartTheme.peso,
  });

  final List<ChartDatum> data;
  final String Function(double) formatValue;

  @override
  State<ShareDonutChart> createState() => _ShareDonutChartState();
}

class _ShareDonutChartState extends State<ShareDonutChart> {
  int _touchedIndex = -1;

  @override
  Widget build(BuildContext context) {
    final total = widget.data.fold<double>(0, (sum, d) => sum + d.value);

    return Row(
      children: [
        Expanded(
          flex: 3,
          child: PieChart(
            PieChartData(
              sectionsSpace: 2,
              centerSpaceRadius: 34,
              startDegreeOffset: -90,
              pieTouchData: PieTouchData(
                touchCallback: (event, response) {
                  setState(() {
                    _touchedIndex =
                        response?.touchedSection?.touchedSectionIndex ?? -1;
                  });
                },
              ),
              sections: [
                for (var i = 0; i < widget.data.length; i++)
                  PieChartSectionData(
                    value: widget.data[i].value,
                    color: widget.data[i].color ?? ChartTheme.seriesColor(i),
                    radius: _touchedIndex == i ? 52 : 44,
                    showTitle: false,
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(width: AppSpacing.lg),
        Expanded(
          flex: 4,
          child: ListView.separated(
            padding: EdgeInsets.zero,
            itemCount: widget.data.length,
            separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
            itemBuilder: (context, index) {
              final datum = widget.data[index];
              final share = total <= 0 ? 0.0 : datum.value / total * 100;
              return Row(
                children: [
                  Container(
                    width: 10,
                    height: 10,
                    decoration: BoxDecoration(
                      color: datum.color ?? ChartTheme.seriesColor(index),
                      borderRadius: BorderRadius.circular(3),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      datum.label,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: ChartTheme.valueLabel,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Text(
                    '${share.toStringAsFixed(0)}%',
                    style: ChartTheme.valueLabel.copyWith(
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Text(
                    widget.formatValue(datum.value),
                    style: ChartTheme.valueLabel,
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }
}
