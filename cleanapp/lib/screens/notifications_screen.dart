import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../models/notification_model.dart';
import '../services/notification_service.dart';
import '../services/auth_service.dart';
import '../theme/app_colors.dart';

// ══════════════════════════════════════════════════════════════════════════════
// NOTIFICATIONS SCREEN
// ══════════════════════════════════════════════════════════════════════════════
class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});
  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabs;
  int _userId = 0;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 3, vsync: this);
    WidgetsBinding.instance.addPostFrameCallback((_) => _init());
  }

  void _init() {
    final ns   = Provider.of<NotificationService>(context, listen: false);
    final user = Provider.of<AuthService>(context, listen: false).currentUser;
    if (user != null) {
      _userId = user.id;
      ns.startPolling(user.id, interval: const Duration(seconds: 15));
    }
  }

  @override
  void dispose() {
    _tabs.dispose();
    // Don't stop polling here — it should continue running in background
    super.dispose();
  }

  Future<void> _refresh() async {
    final ns = Provider.of<NotificationService>(context, listen: false);
    await ns.fetchNotifications(_userId);
  }

  Future<void> _markAllRead() async {
    final ns = Provider.of<NotificationService>(context, listen: false);
    await ns.markAllAsRead(_userId);
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('All notifications marked as read'),
          backgroundColor: Colors.green,
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  // ── Filter helpers ─────────────────────────────────────────────────────────
  List<NotificationModel> _filterByTab(List<NotificationModel> all, int tab) {
    switch (tab) {
      case 1: return all.where((n) => !n.isRead).toList();
      case 2: return all.where((n) => n.isRead).toList();
      default: return all;
    }
  }

  // ── Group by date ──────────────────────────────────────────────────────────
  Map<String, List<NotificationModel>> _groupByDate(List<NotificationModel> list) {
    final now   = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final yest  = today.subtract(const Duration(days: 1));
    final map   = <String, List<NotificationModel>>{};

    for (final n in list) {
      final d = DateTime(n.createdAt.year, n.createdAt.month, n.createdAt.day);
      final String key;
      if (d == today)       key = 'Today';
      else if (d == yest)   key = 'Yesterday';
      else                  key = _formatDate(n.createdAt);
      map.putIfAbsent(key, () => []).add(n);
    }
    return map;
  }

  String _formatDate(DateTime dt) {
    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    return '${dt.day} ${months[dt.month - 1]} ${dt.year}';
  }

  // ══════════════════════════════════════════════════════════════════════════
  // BUILD
  // ══════════════════════════════════════════════════════════════════════════
  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      backgroundColor: isDark ? const Color(0xFF111A19) : AppColors.background,
      appBar: AppBar(
        backgroundColor: isDark ? const Color(0xFF1E2D2C) : Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_rounded),
          onPressed: () => Navigator.canPop(context) ? Navigator.pop(context) : context.go('/home'),
        ),
        title: Consumer<NotificationService>(
          builder: (_, ns, __) => Row(children: [
            const Text('Notifications', style: TextStyle(fontWeight: FontWeight.w800)),
            if (ns.unreadCount > 0) ...[
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: Colors.red, borderRadius: BorderRadius.circular(12)),
                child: Text('${ns.unreadCount}',
                  style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w800)),
              ),
            ],
          ]),
        ),
        actions: [
          Consumer<NotificationService>(
            builder: (_, ns, __) => Row(mainAxisSize: MainAxisSize.min, children: [
              if (ns.unreadCount > 0)
                IconButton(
                  icon: const Icon(Icons.done_all_rounded),
                  tooltip: 'Mark all read',
                  onPressed: _markAllRead,
                ),
              IconButton(
                icon: ns.isLoading
                    ? const SizedBox(width: 18, height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2))
                    : const Icon(Icons.refresh_rounded),
                onPressed: _refresh,
              ),
            ]),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          indicatorColor: AppColors.primary,
          labelColor: AppColors.primary,
          unselectedLabelColor: Colors.grey,
          labelStyle: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          tabs: const [
            Tab(text: 'All'),
            Tab(text: 'Unread'),
            Tab(text: 'Read'),
          ],
        ),
      ),
      body: Consumer<NotificationService>(
        builder: (_, ns, __) {
          if (ns.isLoading && ns.notifications.isEmpty)
            return const Center(child: CircularProgressIndicator());

          return TabBarView(
            controller: _tabs,
            children: List.generate(3, (tab) {
              final filtered = _filterByTab(ns.notifications, tab);
              if (filtered.isEmpty) return _emptyState(tab, isDark);
              return _buildList(filtered, ns, isDark);
            }),
          );
        },
      ),
    );
  }

  // ── List with date grouping ────────────────────────────────────────────────
  Widget _buildList(List<NotificationModel> list, NotificationService ns, bool isDark) {
    final groups = _groupByDate(list);
    final keys   = groups.keys.toList();

    // Build flat items list: header + items per group
    final items = <_ListItem>[];
    for (final key in keys) {
      items.add(_ListItem.header(key));
      for (final n in groups[key]!) items.add(_ListItem.notif(n));
    }

    return RefreshIndicator(
      onRefresh: _refresh,
      child: ListView.builder(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 80),
        itemCount: items.length,
        itemBuilder: (_, i) {
          final item = items[i];
          if (item.isHeader) {
            return _dateHeader(item.header!, isDark)
                .animate().fadeIn(delay: (i * 30).ms);
          }
          final notif = item.notif!;
          return Dismissible(
            key: Key('notif_${notif.id}'),
            direction: notif.isRead
                ? DismissDirection.none
                : DismissDirection.endToStart,
            background: Container(
              alignment: Alignment.centerRight,
              padding: const EdgeInsets.only(right: 20),
              margin: const EdgeInsets.only(bottom: 8),
              decoration: BoxDecoration(
                color: Colors.green.shade100,
                borderRadius: BorderRadius.circular(16)),
              child: const Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  Icon(Icons.done_all_rounded, color: Colors.green),
                  SizedBox(width: 6),
                  Text('Mark read', style: TextStyle(color: Colors.green, fontWeight: FontWeight.w600)),
                  SizedBox(width: 8),
                ],
              ),
            ),
            confirmDismiss: (_) async {
              await ns.markAsRead(notif);
              return false; // don't remove, just mark read
            },
            child: _NotifCard(
              notif: notif,
              isDark: isDark,
              onTap: () {
                ns.markAsRead(notif);
                _showDetail(notif, isDark);
              },
            ).animate().fadeIn(delay: (i * 25).ms).slideX(begin: 0.05),
          );
        },
      ),
    );
  }

  Widget _dateHeader(String label, bool isDark) => Padding(
    padding: const EdgeInsets.fromLTRB(4, 16, 4, 8),
    child: Row(children: [
      Text(label, style: TextStyle(
        fontWeight: FontWeight.w700, fontSize: 12,
        color: isDark ? Colors.white60 : Colors.grey[600])),
      const SizedBox(width: 8),
      Expanded(child: Divider(color: isDark ? Colors.white12 : Colors.grey[300])),
    ]),
  );

  // ── Empty state ────────────────────────────────────────────────────────────
  Widget _emptyState(int tab, bool isDark) {
    final msgs = ['No notifications yet', 'No unread notifications', 'No read notifications'];
    final subs = ['You\'re all caught up!', 'You\'re up to date 👍', 'Nothing here yet'];
    return Center(
      child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
        Container(
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: 0.08),
            shape: BoxShape.circle),
          child: Icon(
            tab == 1 ? Icons.mark_email_read_rounded : Icons.notifications_none_rounded,
            size: 52, color: AppColors.primary.withValues(alpha: 0.5)),
        ),
        const SizedBox(height: 20),
        Text(msgs[tab], style: TextStyle(
          fontSize: 18, fontWeight: FontWeight.w700,
          color: isDark ? Colors.white : AppColors.textPrimary)),
        const SizedBox(height: 6),
        Text(subs[tab], style: const TextStyle(color: Colors.grey)),
        const SizedBox(height: 24),
        OutlinedButton.icon(
          onPressed: _refresh,
          icon: const Icon(Icons.refresh_rounded, size: 16),
          label: const Text('Refresh'),
          style: OutlinedButton.styleFrom(
            foregroundColor: AppColors.primary,
            side: const BorderSide(color: AppColors.primary))),
      ]),
    ).animate().fadeIn(duration: 400.ms);
  }

  // ── Detail bottom sheet ────────────────────────────────────────────────────
  void _showDetail(NotificationModel notif, bool isDark) {
    final color  = _notifColor(notif.notificationType);
    final icon   = _notifIcon(notif.notificationType);

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => DraggableScrollableSheet(
        initialChildSize: 0.5,
        maxChildSize: 0.85,
        minChildSize: 0.35,
        builder: (_, ctrl) => Container(
          decoration: BoxDecoration(
            color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
            borderRadius: const BorderRadius.vertical(top: Radius.circular(24))),
          child: Column(children: [
            // Handle
            Container(width: 40, height: 4, margin: const EdgeInsets.symmetric(vertical: 12),
              decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2))),
            Expanded(
              child: ListView(controller: ctrl, padding: const EdgeInsets.fromLTRB(20, 4, 20, 30), children: [
                // Icon + type badge
                Row(children: [
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(14)),
                    child: Icon(icon, color: color, size: 28)),
                  const SizedBox(width: 14),
                  Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(20)),
                      child: Text(notif.notificationType,
                        style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w700))),
                    const SizedBox(height: 6),
                    Text(_timeAgo(notif.createdAt),
                      style: const TextStyle(color: Colors.grey, fontSize: 12)),
                  ])),
                  if (notif.isRead)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: Colors.green.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(color: Colors.green.withValues(alpha: 0.3))),
                      child: const Row(mainAxisSize: MainAxisSize.min, children: [
                        Icon(Icons.check_circle_outline, size: 12, color: Colors.green),
                        SizedBox(width: 4),
                        Text('Read', style: TextStyle(color: Colors.green, fontSize: 10)),
                      ])),
                ]),
                const SizedBox(height: 20),

                // Title
                Text(notif.title, style: TextStyle(
                  fontSize: 20, fontWeight: FontWeight.w800,
                  color: isDark ? Colors.white : AppColors.textPrimary)),
                const SizedBox(height: 12),

                // Body
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: isDark ? Colors.white.withValues(alpha: 0.05) : Colors.grey[50],
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: isDark ? Colors.white12 : Colors.grey.shade200)),
                  child: Text(notif.body, style: TextStyle(
                    fontSize: 14, height: 1.6,
                    color: isDark ? Colors.white70 : AppColors.textSecondary))),
                const SizedBox(height: 16),

                // Request number chip
                if (notif.requestNumber != null) ...[
                  Row(children: [
                    const Icon(Icons.receipt_long_rounded, size: 14, color: AppColors.primary),
                    const SizedBox(width: 6),
                    Text('Request: ', style: const TextStyle(color: Colors.grey, fontSize: 12)),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(20)),
                      child: Text(notif.requestNumber!,
                        style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700, fontSize: 12))),
                  ]),
                  const SizedBox(height: 8),
                ],

                // Timestamp
                Row(children: [
                  const Icon(Icons.access_time_rounded, size: 14, color: Colors.grey),
                  const SizedBox(width: 6),
                  Text(_fullDate(notif.createdAt),
                    style: const TextStyle(color: Colors.grey, fontSize: 12)),
                ]),

                // Action button for 'Action' type
                if (notif.notificationType == 'Action' && notif.transportRequestId != null) ...[
                  const SizedBox(height: 24),
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      onPressed: () {
                        Navigator.pop(context);
                        context.push('/driver-trips');
                      },
                      icon: const Icon(Icons.local_shipping_rounded, size: 18),
                      label: const Text('View Transport Jobs'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14))),
                    ),
                  ),
                ],
              ]),
            ),
          ]),
        ),
      ),
    );
  }

  String _timeAgo(DateTime dt) {
    final diff = DateTime.now().difference(dt);
    if (diff.inSeconds < 60)  return 'Just now';
    if (diff.inMinutes < 60)  return '${diff.inMinutes}m ago';
    if (diff.inHours < 24)    return '${diff.inHours}h ago';
    if (diff.inDays == 1)     return 'Yesterday';
    return '${diff.inDays}d ago';
  }

  String _fullDate(DateTime dt) {
    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    final h = dt.hour.toString().padLeft(2,'0');
    final m = dt.minute.toString().padLeft(2,'0');
    return '${dt.day} ${months[dt.month-1]} ${dt.year}  •  $h:$m';
  }

  Color _notifColor(String type) => switch (type) {
    'Action'  => Colors.orange,
    'Success' => Colors.green,
    'Warning' => Colors.red,
    _         => Colors.blue,
  };

  IconData _notifIcon(String type) => switch (type) {
    'Action'  => Icons.touch_app_rounded,
    'Success' => Icons.check_circle_rounded,
    'Warning' => Icons.warning_amber_rounded,
    _         => Icons.info_rounded,
  };
}

// ══════════════════════════════════════════════════════════════════════════════
// NOTIFICATION CARD
// ══════════════════════════════════════════════════════════════════════════════
class _NotifCard extends StatelessWidget {
  final NotificationModel notif;
  final bool isDark;
  final VoidCallback onTap;
  const _NotifCard({required this.notif, required this.isDark, required this.onTap});

  Color get _color => switch (notif.notificationType) {
    'Action'  => Colors.orange,
    'Success' => Colors.green,
    'Warning' => Colors.red,
    _         => Colors.blue,
  };

  IconData get _icon => switch (notif.notificationType) {
    'Action'  => Icons.touch_app_rounded,
    'Success' => Icons.check_circle_rounded,
    'Warning' => Icons.warning_amber_rounded,
    _         => Icons.info_rounded,
  };

  String _timeAgo(DateTime dt) {
    final diff = DateTime.now().difference(dt);
    if (diff.inSeconds < 60)  return 'Just now';
    if (diff.inMinutes < 60)  return '${diff.inMinutes}m ago';
    if (diff.inHours < 24)    return '${diff.inHours}h ago';
    if (diff.inDays == 1)     return 'Yesterday';
    return '${diff.inDays}d ago';
  }

  @override
  Widget build(BuildContext context) {
    final unread = !notif.isRead;

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: unread
            ? (isDark ? const Color(0xFF1E3230) : _color.withValues(alpha: 0.04))
            : (isDark ? const Color(0xFF1A2826) : Colors.white),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: unread ? _color.withValues(alpha: 0.25) : (isDark ? Colors.white10 : Colors.grey.shade200),
          width: unread ? 1.5 : 1),
        boxShadow: unread ? [BoxShadow(color: _color.withValues(alpha: 0.08), blurRadius: 8, offset: const Offset(0,3))] : [],
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [

            // Icon circle
            Stack(children: [
              Container(
                width: 46, height: 46,
                decoration: BoxDecoration(
                  color: _color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(13)),
                child: Icon(_icon, color: _color, size: 22)),
              if (unread)
                Positioned(top: 0, right: 0,
                  child: Container(
                    width: 10, height: 10,
                    decoration: BoxDecoration(
                      color: _color, shape: BoxShape.circle,
                      border: Border.all(color: Colors.white, width: 1.5)))),
            ]),
            const SizedBox(width: 12),

            // Content
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              // Title row
              Row(children: [
                Expanded(child: Text(notif.title,
                  style: TextStyle(
                    fontWeight: unread ? FontWeight.w800 : FontWeight.w600,
                    fontSize: 14,
                    color: isDark ? Colors.white : AppColors.textPrimary),
                  maxLines: 1, overflow: TextOverflow.ellipsis)),
                Text(_timeAgo(notif.createdAt),
                  style: TextStyle(fontSize: 10, color: Colors.grey[400])),
              ]),
              const SizedBox(height: 4),

              // Body
              Text(notif.body,
                style: TextStyle(
                  fontSize: 12.5, height: 1.4,
                  color: isDark ? Colors.white60 : AppColors.textSecondary),
                maxLines: 2, overflow: TextOverflow.ellipsis),
              const SizedBox(height: 6),

              // Footer row
              Row(children: [
                // Type badge
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                  decoration: BoxDecoration(
                    color: _color.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(10)),
                  child: Text(notif.notificationType,
                    style: TextStyle(color: _color, fontSize: 9, fontWeight: FontWeight.w700))),
                if (notif.requestNumber != null) ...[
                  const SizedBox(width: 6),
                  Icon(Icons.receipt_rounded, size: 10, color: Colors.grey[400]),
                  const SizedBox(width: 2),
                  Text(notif.requestNumber!, style: TextStyle(
                    fontSize: 10, color: _color, fontWeight: FontWeight.w600)),
                ],
                const Spacer(),
                if (notif.notificationType == 'Action' && !notif.isRead)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                    decoration: BoxDecoration(
                      color: Colors.orange.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(color: Colors.orange.withValues(alpha: 0.3))),
                    child: const Text('Tap to act',
                      style: TextStyle(color: Colors.orange, fontSize: 9, fontWeight: FontWeight.w600))),
              ]),
            ])),
          ]),
        ),
      ),
    );
  }
}

// ── Internal list item model ──────────────────────────────────────────────────
class _ListItem {
  final bool isHeader;
  final String? header;
  final NotificationModel? notif;

  _ListItem.header(this.header) : isHeader = true, notif = null;
  _ListItem.notif(this.notif) : isHeader = false, header = null;
}
