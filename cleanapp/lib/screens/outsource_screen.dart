import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:flutter_animate/flutter_animate.dart';
import '../services/auth_service.dart';
import '../services/api_service.dart';
import '../models/submission_model.dart';
import '../theme/app_colors.dart';
import 'profile_screen.dart';

// ══════════════════════════════════════════════════════════════════════════════
// OUTSOURCE SCREEN
// ══════════════════════════════════════════════════════════════════════════════
class OutsourceScreen extends StatefulWidget {
  const OutsourceScreen({super.key});
  @override
  State<OutsourceScreen> createState() => _OutsourceScreenState();
}

class _OutsourceScreenState extends State<OutsourceScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabs;
  final _api = ApiService();
  int _currentIndex = 0; // 0 = main tabs, 1 = profile

  List<Map<String, dynamic>> _receipts = [];
  List<Map<String, dynamic>> _weredas  = [];
  List<Map<String, dynamic>> _vehicles = [];

  bool _loading    = true;
  bool _submitting = false;

  // Form
  final _formKey   = GlobalKey<FormState>();
  int? _weredaId;
  int? _vehicleId;
  final _kgCtrl    = TextEditingController();
  final _priceCtrl = TextEditingController(text: '1.40');
  final _notesCtrl = TextEditingController();
  DateTime  _date  = DateTime.now();
  TimeOfDay _time  = TimeOfDay.now();

  // ── lifecycle ─────────────────────────────────────────────────────────────
  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 3, vsync: this);
    _loadAll();
  }

  @override
  void dispose() {
    _tabs.dispose();
    _kgCtrl.dispose();
    _priceCtrl.dispose();
    _notesCtrl.dispose();
    super.dispose();
  }

  // ── data ──────────────────────────────────────────────────────────────────
  Future<void> _loadAll() async {
    setState(() => _loading = true);
    final user = Provider.of<AuthService>(context, listen: false).currentUser!;

    final results = await Future.wait([
      _api.getWeredas(),
      _api.getVehicles(),
      _api.getHistory(user.id),
    ]);
    if (!mounted) return;

    final weredas  = results[0] as List<Map<String, dynamic>>;
    final vehicles = results[1] as List<Map<String, dynamic>>;
    final history  = results[2] as List<SubmissionModel>;

    setState(() {
      _weredas  = weredas;
      _vehicles = vehicles;
      _receipts = history.map<Map<String, dynamic>>((s) => {
        'wereda_name':  s.weredaName  ?? '—',
        'company_name': s.mahberatName ?? '—',
        'receipt_date': s.date,
        'kilogram':     s.kilogram,
        'rate':         s.rate,
        'total_amount': s.total,
        'status':       s.status,
        'notes':        s.notes,
      }).toList();
      _loading = false;
    });
  }

  // ── submit ────────────────────────────────────────────────────────────────
  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    if (_weredaId == null) { _snack('Please select a Wereda', Colors.red); return; }
    setState(() => _submitting = true);

    final user = Provider.of<AuthService>(context, listen: false).currentUser!;
    final kg    = double.tryParse(_kgCtrl.text)    ?? 0.0;
    final price = double.tryParse(_priceCtrl.text) ?? 1.4;

    final ok = await _api.submitWork(SubmissionModel(
      userId:    user.id,
      role:      user.role,
      weredaId:  _weredaId!,
      mahberatId: 0,
      vehicleId:  _vehicleId ?? user.vehicleId,
      kilogram:   kg,
      rate:       price,
      total:      kg * price,
      date:       DateFormat('yyyy-MM-dd').format(_date),
      time:       '${_time.hour.toString().padLeft(2,'0')}:${_time.minute.toString().padLeft(2,'0')}',
      notes:      _notesCtrl.text,
      receiptType: 'Outsource',
    ));

    setState(() => _submitting = false);
    if (!mounted) return;
    if (ok) {
      _snack('Receipt submitted successfully!', Colors.green);
      _kgCtrl.clear(); _notesCtrl.clear();
      setState(() { _weredaId = null; _vehicleId = null; _date = DateTime.now(); _time = TimeOfDay.now(); });
      await _loadAll();
      _tabs.animateTo(1);
    } else {
      _snack('Submission failed. Please try again.', Colors.red);
    }
  }

  void _snack(String msg, Color c) => ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(content: Text(msg), backgroundColor: c, behavior: SnackBarBehavior.floating));

  // ── computed stats ─────────────────────────────────────────────────────────
  double get _totalKg  => _receipts.fold(0, (s, r) => s + (double.tryParse(r['kilogram']?.toString()    ?? '0') ?? 0));
  double get _totalEtb => _receipts.fold(0, (s, r) => s + (double.tryParse(r['total_amount']?.toString() ?? '0') ?? 0));
  int    get _pendingCnt => _receipts.where((r) => (r['status'] ?? '') == 'Pending').length;
  int    get _approvedCnt => _receipts.where((r) => ['Approved','Paid'].contains(r['status'] ?? '')).length;

  double get _thisMonthKg {
    final now = DateTime.now();
    return _receipts.fold(0, (s, r) {
      final raw = r['receipt_date']?.toString() ?? '';
      if (raw.length < 7) return s;
      final p = raw.split('-');
      if (p.length >= 2 && int.tryParse(p[0]) == now.year && int.tryParse(p[1]) == now.month)
        return s + (double.tryParse(r['kilogram']?.toString() ?? '0') ?? 0);
      return s;
    });
  }

  // ══════════════════════════════════════════════════════════════════════════
  // BUILD
  // ══════════════════════════════════════════════════════════════════════════
  @override
  Widget build(BuildContext context) {
    final user   = Provider.of<AuthService>(context).currentUser!;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      backgroundColor: isDark ? const Color(0xFF111A19) : AppColors.background,
      appBar: AppBar(
        backgroundColor: isDark ? const Color(0xFF1E2D2C) : Colors.white,
        elevation: 0,
        title: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Outsource Company', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
          Text(user.name, style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w400)),
        ]),
        bottom: _currentIndex == 0
            ? TabBar(
                controller: _tabs,
                indicatorColor: AppColors.primary,
                labelColor: AppColors.primary,
                unselectedLabelColor: Colors.grey,
                labelStyle: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
                tabs: const [
                  Tab(icon: Icon(Icons.dashboard_outlined, size: 18),    text: 'Dashboard'),
                  Tab(icon: Icon(Icons.add_circle_outline, size: 18),     text: 'Submit'),
                  Tab(icon: Icon(Icons.history_rounded, size: 18),        text: 'History'),
                ],
              )
            : null,
        actions: [
          IconButton(
            icon: const Icon(Icons.settings_rounded),
            onPressed: () => context.push('/settings'),
            tooltip: 'Settings',
          ),
          IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: _loadAll),
        ],
      ),
      body: _currentIndex == 1
          ? const ProfileScreen()
          : _loading
              ? const Center(child: CircularProgressIndicator())
              : TabBarView(
                  controller: _tabs,
                  children: [
                    _buildDashboard(isDark),
                    _buildSubmitTab(isDark),
                    _buildHistoryTab(isDark),
                  ],
                ),
      bottomNavigationBar: _OutsourceBottomNav(
        currentIndex: _currentIndex,
        isDark: isDark,
        onTap: (i) => setState(() => _currentIndex = i),
      ),
    );
  }

  // ══════════════════════════════════════════════════════════════════════════
  // TAB 0 — DASHBOARD
  // ══════════════════════════════════════════════════════════════════════════
  Widget _buildDashboard(bool isDark) {
    final user = Provider.of<AuthService>(context, listen: false).currentUser!;

    return RefreshIndicator(
      onRefresh: _loadAll,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
        children: [

          // ── Hero gradient card ───────────────────────────────────────────
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              gradient: AppColors.cardGradient,
              borderRadius: BorderRadius.circular(20),
              boxShadow: [BoxShadow(color: AppColors.primary.withValues(alpha: 0.3), blurRadius: 16, offset: const Offset(0,6))],
            ),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(12)),
                  child: const Icon(Icons.recycling_rounded, color: Colors.white, size: 26)),
                const SizedBox(width: 14),
                Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(user.name, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 16)),
                  const Text('Outsource Company Representative', style: TextStyle(color: Colors.white70, fontSize: 11)),
                ])),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(20)),
                  child: Text('${_receipts.length} receipts',
                    style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w600))),
              ]),
              const SizedBox(height: 20),
              Row(children: [
                _heroStat('Total KG',    '${_totalKg.toStringAsFixed(1)}',       Icons.scale_rounded),
                const SizedBox(width: 8),
                _heroStat('Total ETB',   _totalEtb.toStringAsFixed(0),           Icons.payments_rounded),
                const SizedBox(width: 8),
                _heroStat('This Month',  '${_thisMonthKg.toStringAsFixed(1)} kg', Icons.calendar_month_rounded),
              ]),
            ]),
          ).animate().fadeIn(duration: 300.ms).slideY(begin: -0.05),

          const SizedBox(height: 20),

          // ── Quick stat cards ─────────────────────────────────────────────
          Row(children: [
            Expanded(child: _quickStat('Submissions', '${_receipts.length}', Icons.receipt_long_rounded, Colors.blue, isDark)),
            const SizedBox(width: 12),
            Expanded(child: _quickStat('Pending',     '$_pendingCnt',        Icons.hourglass_empty_rounded, Colors.orange, isDark)),
            const SizedBox(width: 12),
            Expanded(child: _quickStat('Approved',    '$_approvedCnt',       Icons.check_circle_rounded, Colors.green, isDark)),
          ]).animate().fadeIn(delay: 100.ms),

          const SizedBox(height: 24),

          // ── Quick actions ────────────────────────────────────────────────
          Text('Quick Actions', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700,
            color: isDark ? Colors.white : AppColors.textPrimary)),
          const SizedBox(height: 12),
          Row(children: [
            Expanded(child: _actionBtn(Icons.add_circle_rounded, 'New Receipt', AppColors.primary, isDark, () => _tabs.animateTo(1))),
            const SizedBox(width: 12),
            Expanded(child: _actionBtn(Icons.history_rounded, 'View History', Colors.blue, isDark, () => _tabs.animateTo(2))),
          ]).animate().fadeIn(delay: 150.ms),

          const SizedBox(height: 24),

          // ── Performance chart (bar-style) ────────────────────────────────
          if (_receipts.isNotEmpty) ...[
            Text('Recent Activity', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700,
              color: isDark ? Colors.white : AppColors.textPrimary)),
            const SizedBox(height: 12),
            _buildMiniBarChart(isDark).animate().fadeIn(delay: 200.ms),
            const SizedBox(height: 24),
          ],

          // ── Recent receipts ──────────────────────────────────────────────
          if (_receipts.isNotEmpty) ...[
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
              Text('Recent Receipts', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700,
                color: isDark ? Colors.white : AppColors.textPrimary)),
              TextButton(
                onPressed: () => _tabs.animateTo(2),
                child: const Text('See All', style: TextStyle(color: AppColors.primary))),
            ]),
            const SizedBox(height: 8),
            ..._receipts.take(3).toList().asMap().entries.map((e) =>
              _receiptTile(e.value, isDark)
                .animate().fadeIn(delay: (220 + e.key * 60).ms).slideX(begin: 0.05)),
          ],

          if (_receipts.isEmpty) _emptyCard(isDark).animate().fadeIn(delay: 200.ms),
        ],
      ),
    );
  }

  // mini bar chart — last 6 submissions KG
  Widget _buildMiniBarChart(bool isDark) {
    final last6 = _receipts.take(6).toList().reversed.toList();
    if (last6.isEmpty) return const SizedBox.shrink();
    final maxKg = last6.fold(0.0, (m, r) => (double.tryParse(r['kilogram']?.toString() ?? '0') ?? 0) > m
        ? (double.tryParse(r['kilogram']?.toString() ?? '0') ?? 0) : m);
    if (maxKg == 0) return const SizedBox.shrink();

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
        borderRadius: BorderRadius.circular(16),
        boxShadow: [BoxShadow(color: AppColors.shadow, blurRadius: 8, offset: const Offset(0,3))]),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text('Last ${last6.length} Submissions (KG)',
          style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12, color: Colors.grey)),
        const SizedBox(height: 14),
        SizedBox(
          height: 80,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: last6.asMap().entries.map((e) {
              final kg     = double.tryParse(e.value['kilogram']?.toString() ?? '0') ?? 0;
              final ratio  = maxKg > 0 ? kg / maxKg : 0.0;
              final status = e.value['status']?.toString() ?? '';
              final color  = status == 'Approved' || status == 'Paid' ? Colors.green
                  : status == 'Pending' ? Colors.orange : AppColors.primary;
              final date   = (e.value['receipt_date']?.toString() ?? '').length >= 10
                  ? e.value['receipt_date'].toString().substring(5, 10) : '—';
              return Expanded(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 3),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.center, children: [
                    Text('${kg.toStringAsFixed(0)}',
                      style: TextStyle(fontSize: 8, color: color, fontWeight: FontWeight.w700)),
                    const SizedBox(height: 2),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(4),
                      child: FractionallySizedBox(
                        heightFactor: ratio.clamp(0.05, 1.0),
                        child: Container(
                          height: double.infinity,
                          decoration: BoxDecoration(
                            color: color,
                            borderRadius: BorderRadius.circular(4))))),
                    const SizedBox(height: 4),
                    Text(date, style: const TextStyle(fontSize: 7, color: Colors.grey)),
                  ]),
                ),
              );
            }).toList(),
          ),
        ),
      ]),
    );
  }

  Widget _heroStat(String label, String value, IconData icon) => Expanded(
    child: Container(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 8),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.15), borderRadius: BorderRadius.circular(12)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(icon, color: Colors.white70, size: 16),
        const SizedBox(height: 5),
        Text(value, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 15)),
        Text(label, style: TextStyle(color: Colors.white.withValues(alpha: 0.7), fontSize: 9)),
      ]),
    ),
  );

  Widget _quickStat(String label, String value, IconData icon, Color color, bool isDark) => Container(
    padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 10),
    decoration: BoxDecoration(
      color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
      borderRadius: BorderRadius.circular(16),
      boxShadow: [BoxShadow(color: AppColors.shadow, blurRadius: 8, offset: const Offset(0,3))]),
    child: Column(children: [
      Container(
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(color: color.withValues(alpha: 0.12), shape: BoxShape.circle),
        child: Icon(icon, color: color, size: 20)),
      const SizedBox(height: 8),
      Text(value, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 20,
        color: isDark ? Colors.white : AppColors.textPrimary)),
      const SizedBox(height: 2),
      Text(label, style: const TextStyle(fontSize: 10, color: Colors.grey), textAlign: TextAlign.center),
    ]),
  );

  Widget _actionBtn(IconData icon, String label, Color color, bool isDark, VoidCallback onTap) =>
    GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 16),
        decoration: BoxDecoration(
          color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
          borderRadius: BorderRadius.circular(14),
          boxShadow: [BoxShadow(color: AppColors.shadow, blurRadius: 6)]),
        child: Column(children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
            child: Icon(icon, color: color, size: 24)),
          const SizedBox(height: 8),
          Text(label, style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12,
            color: isDark ? Colors.white : AppColors.textPrimary)),
        ]),
      ),
    );

  Widget _emptyCard(bool isDark) => Container(
    width: double.infinity,
    padding: const EdgeInsets.all(32),
    decoration: BoxDecoration(
      color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
      borderRadius: BorderRadius.circular(16)),
    child: Column(children: [
      Icon(Icons.receipt_long_outlined, size: 56, color: Colors.grey[300]),
      const SizedBox(height: 12),
      const Text('No receipts yet', style: TextStyle(color: Colors.grey, fontSize: 15, fontWeight: FontWeight.w600)),
      const SizedBox(height: 6),
      const Text('Submit your first collection receipt', style: TextStyle(color: Colors.grey, fontSize: 12)),
      const SizedBox(height: 16),
      ElevatedButton.icon(
        onPressed: () => _tabs.animateTo(1),
        icon: const Icon(Icons.add, size: 16),
        label: const Text('Submit Receipt'),
        style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary, foregroundColor: Colors.white)),
    ]),
  );

  Widget _receiptTile(Map<String, dynamic> r, bool isDark) {
    final total  = double.tryParse(r['total_amount']?.toString() ?? '0') ?? 0;
    final kg     = double.tryParse(r['kilogram']?.toString()    ?? '0') ?? 0;
    final date   = (r['receipt_date']?.toString() ?? '').length >= 10
        ? r['receipt_date'].toString().substring(0, 10) : '—';
    final status = r['status']?.toString() ?? 'Pending';
    final sColor = status == 'Paid' ? Colors.green : status == 'Approved' ? Colors.blue : Colors.orange;

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
        borderRadius: BorderRadius.circular(12),
        boxShadow: [BoxShadow(color: AppColors.shadow, blurRadius: 4)]),
      child: Row(children: [
        const Icon(Icons.recycling_rounded, color: AppColors.primary, size: 20),
        const SizedBox(width: 10),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(r['wereda_name']?.toString() ?? '—',
            style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13,
              color: isDark ? Colors.white : AppColors.textPrimary)),
          Text('$date  •  ${kg.toStringAsFixed(1)} kg',
            style: const TextStyle(color: Colors.grey, fontSize: 11)),
        ])),
        Column(crossAxisAlignment: CrossAxisAlignment.end, children: [
          Text('ETB ${total.toStringAsFixed(2)}',
            style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w700, fontSize: 13)),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(color: sColor.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(10)),
            child: Text(status, style: TextStyle(color: sColor, fontSize: 9, fontWeight: FontWeight.w700))),
        ]),
      ]),
    );
  }

  // ══════════════════════════════════════════════════════════════════════════
  // TAB 1 — SUBMIT RECEIPT
  // ══════════════════════════════════════════════════════════════════════════
  Widget _buildSubmitTab(bool isDark) {
    final user = Provider.of<AuthService>(context, listen: false).currentUser!;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [

          // Header card
          Container(
            width: double.infinity,
            margin: const EdgeInsets.only(bottom: 20),
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(gradient: AppColors.cardGradient, borderRadius: BorderRadius.circular(16)),
            child: Row(children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(10)),
                child: const Icon(Icons.recycling_rounded, color: Colors.white, size: 22)),
              const SizedBox(width: 12),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(user.name, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 14)),
                const Text('New Collection Receipt', style: TextStyle(color: Colors.white70, fontSize: 11)),
              ])),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(20)),
                child: Text('${_receipts.length} total',
                  style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w600))),
            ]),
          ),

          // ── Location ──────────────────────────────────────────────────────
          _sectionHeader('📍 Location', isDark),
          const SizedBox(height: 10),

          _fieldLabel('Wereda *'),
          _styledDropdown<int?>(
            hint: '— Select Wereda —', value: _weredaId,
            items: _weredas.map((w) => DropdownMenuItem<int?>(
              value: w['id'] as int, child: Text(w['name']?.toString() ?? ''))).toList(),
            onChanged: (v) => setState(() => _weredaId = v), isDark: isDark),
          const SizedBox(height: 14),

          _fieldLabel('Vehicle (optional)'),
          _styledDropdown<int?>(
            hint: '— No vehicle —', value: _vehicleId,
            items: [
              const DropdownMenuItem(value: null, child: Text('— No vehicle —')),
              ..._vehicles.map((v) => DropdownMenuItem<int?>(
                value: v['id'] as int, child: Text(v['name']?.toString() ?? ''))),
            ],
            onChanged: (v) => setState(() => _vehicleId = v), isDark: isDark),

          const SizedBox(height: 20),

          // ── Date & Time ───────────────────────────────────────────────────
          _sectionHeader('📅 Date & Time', isDark),
          const SizedBox(height: 10),
          Row(children: [
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _fieldLabel('Date *'),
              _tapField(DateFormat('dd MMM yyyy').format(_date), Icons.calendar_today_rounded,
                onTap: () async {
                  final p = await showDatePicker(context: context, initialDate: _date,
                    firstDate: DateTime(2024), lastDate: DateTime.now().add(const Duration(days: 1)));
                  if (p != null) setState(() => _date = p);
                }, isDark: isDark),
            ])),
            const SizedBox(width: 12),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _fieldLabel('Time *'),
              _tapField(
                '${_time.hour.toString().padLeft(2,'0')}:${_time.minute.toString().padLeft(2,'0')}',
                Icons.access_time_rounded,
                onTap: () async {
                  final p = await showTimePicker(context: context, initialTime: _time);
                  if (p != null) setState(() => _time = p);
                }, isDark: isDark),
            ])),
          ]),

          const SizedBox(height: 20),

          // ── Weight & Price ────────────────────────────────────────────────
          _sectionHeader('⚖️ Weight & Price', isDark),
          const SizedBox(height: 10),
          Row(children: [
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _fieldLabel('Kilogram *'),
              TextFormField(
                controller: _kgCtrl,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: _inputDeco('0.00', Icons.scale_rounded, isDark),
                validator: (v) => (v == null || (double.tryParse(v) ?? 0) <= 0) ? 'Enter valid KG' : null,
                onChanged: (_) => setState(() {})),
            ])),
            const SizedBox(width: 12),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _fieldLabel('Price / KG (ETB) *'),
              TextFormField(
                controller: _priceCtrl,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: _inputDeco('1.40', Icons.sell_rounded, isDark),
                validator: (v) => (v == null || (double.tryParse(v) ?? 0) <= 0) ? 'Enter price' : null,
                onChanged: (_) => setState(() {})),
            ])),
          ]),
          const SizedBox(height: 14),

          // Total
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(vertical: 18),
            decoration: BoxDecoration(
              gradient: LinearGradient(colors: [
                AppColors.primary.withValues(alpha: 0.08), AppColors.secondary.withValues(alpha: 0.05)]),
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: AppColors.primary.withValues(alpha: 0.3))),
            child: Column(children: [
              Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                const Icon(Icons.payments_rounded, color: AppColors.primary, size: 18),
                const SizedBox(width: 6),
                const Text('Total Amount', style: TextStyle(fontSize: 12, color: Colors.grey, fontWeight: FontWeight.w600)),
              ]),
              const SizedBox(height: 6),
              Text(
                'ETB  ${((double.tryParse(_kgCtrl.text) ?? 0) * (double.tryParse(_priceCtrl.text) ?? 0)).toStringAsFixed(2)}',
                style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w900, color: AppColors.primary)),
              Text('${_kgCtrl.text.isEmpty ? "0" : _kgCtrl.text} kg  ×  ${_priceCtrl.text.isEmpty ? "0" : _priceCtrl.text} ETB',
                style: const TextStyle(fontSize: 11, color: Colors.grey)),
            ]),
          ),

          const SizedBox(height: 20),

          // ── Notes ─────────────────────────────────────────────────────────
          _sectionHeader('📝 Notes', isDark),
          const SizedBox(height: 10),
          TextFormField(
            controller: _notesCtrl, maxLines: 3,
            decoration: _inputDeco('Any additional notes about this collection...', Icons.notes_rounded, isDark)),

          const SizedBox(height: 28),

          SizedBox(
            width: double.infinity, height: 54,
            child: ElevatedButton.icon(
              onPressed: _submitting ? null : _submit,
              icon: _submitting
                  ? const SizedBox(width: 18, height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Icon(Icons.send_rounded, size: 20),
              label: Text(_submitting ? 'Submitting…' : 'Submit Receipt',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary, foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)), elevation: 3)),
          ),
          const SizedBox(height: 50),
        ]),
      ),
    );
  }

  // ══════════════════════════════════════════════════════════════════════════
  // TAB 2 — HISTORY
  // ══════════════════════════════════════════════════════════════════════════
  Widget _buildHistoryTab(bool isDark) {
    if (_receipts.isEmpty) {
      return Center(child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
        Icon(Icons.receipt_long_outlined, size: 72, color: Colors.grey[300]),
        const SizedBox(height: 16),
        const Text('No receipts yet', style: TextStyle(color: Colors.grey, fontSize: 16, fontWeight: FontWeight.w600)),
        const SizedBox(height: 6),
        const Text('Submit your first collection receipt', style: TextStyle(color: Colors.grey, fontSize: 12)),
        const SizedBox(height: 20),
        ElevatedButton.icon(
          onPressed: () => _tabs.animateTo(1),
          icon: const Icon(Icons.add, size: 16), label: const Text('Submit Receipt'),
          style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary, foregroundColor: Colors.white)),
      ]));
    }

    final approvedKg = _receipts
        .where((r) => ['Approved','Paid'].contains(r['status'] ?? ''))
        .fold(0.0, (s, r) => s + (double.tryParse(r['kilogram']?.toString() ?? '0') ?? 0));

    return Column(children: [
      // Summary bar
      Container(
        color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceAround, children: [
          _summaryChip('Total',       '${_receipts.length}',                    Icons.receipt_long_rounded, Colors.blue),
          _summaryChip('KG Total',    '${_totalKg.toStringAsFixed(1)}',          Icons.scale_rounded, AppColors.primary),
          _summaryChip('Approved KG', '${approvedKg.toStringAsFixed(1)}',        Icons.check_circle_rounded, Colors.green),
          _summaryChip('Pending',     '$_pendingCnt',                            Icons.hourglass_empty_rounded, Colors.orange),
        ]),
      ),
      const Divider(height: 1),

      Expanded(
        child: RefreshIndicator(
          onRefresh: _loadAll,
          child: ListView.builder(
            padding: const EdgeInsets.all(12),
            itemCount: _receipts.length,
            itemBuilder: (_, i) => _receiptCard(_receipts[i], isDark, i)
                .animate().fadeIn(delay: (i * 40).ms).slideY(begin: 0.04),
          ),
        ),
      ),
    ]);
  }

  Widget _summaryChip(String label, String val, IconData icon, Color color) => Column(children: [
    Icon(icon, color: color, size: 16),
    const SizedBox(height: 3),
    Text(val, style: TextStyle(color: color, fontWeight: FontWeight.w800, fontSize: 14)),
    Text(label, style: const TextStyle(color: Colors.grey, fontSize: 9)),
  ]);

  Widget _receiptCard(Map<String, dynamic> r, bool isDark, int idx) {
    final total  = double.tryParse(r['total_amount']?.toString() ?? '0') ?? 0;
    final kg     = double.tryParse(r['kilogram']?.toString()    ?? '0') ?? 0;
    final price  = double.tryParse(r['rate']?.toString()         ?? '0') ?? 0;
    final date   = (r['receipt_date']?.toString() ?? '').length >= 10
        ? r['receipt_date'].toString().substring(0, 10) : '—';
    final status = r['status']?.toString() ?? 'Pending';
    final sColor = status == 'Paid' ? Colors.green : status == 'Approved' ? Colors.blue
        : status == 'Rejected' ? Colors.red : Colors.orange;
    final wereda = r['wereda_name']?.toString() ?? '—';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
      elevation: 1,
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => _showDetail(r, isDark),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Container(
                width: 40, height: 40,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(10)),
                child: Center(child: Text('${idx + 1}',
                  style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w800, fontSize: 14)))),
              const SizedBox(width: 12),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(wereda, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14,
                  color: isDark ? Colors.white : AppColors.textPrimary)),
                Text(date, style: const TextStyle(color: Colors.grey, fontSize: 11)),
              ])),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: sColor.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(20)),
                child: Text(status, style: TextStyle(color: sColor, fontSize: 10, fontWeight: FontWeight.w700))),
            ]),
            const Divider(height: 16),
            Row(mainAxisAlignment: MainAxisAlignment.spaceAround, children: [
              _infoChip('KG',    '${kg.toStringAsFixed(1)} kg',       Icons.scale_rounded,   Colors.blue),
              _infoChip('Rate',  'ETB ${price.toStringAsFixed(2)}/kg', Icons.sell_rounded,    Colors.purple),
              _infoChip('Total', 'ETB ${total.toStringAsFixed(2)}',   Icons.payments_rounded, AppColors.primary),
            ]),
          ]),
        ),
      ),
    );
  }

  void _showDetail(Map<String, dynamic> r, bool isDark) {
    final total  = double.tryParse(r['total_amount']?.toString() ?? '0') ?? 0;
    final kg     = double.tryParse(r['kilogram']?.toString()    ?? '0') ?? 0;
    final price  = double.tryParse(r['rate']?.toString()         ?? '0') ?? 0;
    final status = r['status']?.toString() ?? '—';
    final sColor = status == 'Paid' ? Colors.green : status == 'Approved' ? Colors.blue
        : status == 'Rejected' ? Colors.red : Colors.orange;

    showModalBottomSheet(
      context: context, isScrollControlled: true, backgroundColor: Colors.transparent,
      builder: (_) => DraggableScrollableSheet(
        initialChildSize: 0.55, maxChildSize: 0.88, minChildSize: 0.35,
        builder: (_, ctrl) => Container(
          decoration: BoxDecoration(
            color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
            borderRadius: const BorderRadius.vertical(top: Radius.circular(24))),
          child: Column(children: [
            Container(width: 40, height: 4, margin: const EdgeInsets.symmetric(vertical: 12),
              decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2))),
            Expanded(child: ListView(controller: ctrl, padding: const EdgeInsets.fromLTRB(20, 0, 20, 30), children: [
              Row(children: [
                const Icon(Icons.recycling_rounded, color: AppColors.primary, size: 24),
                const SizedBox(width: 10),
                Expanded(child: Text(r['wereda_name']?.toString() ?? '—',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800,
                    color: isDark ? Colors.white : AppColors.textPrimary))),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(color: sColor.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(20)),
                  child: Text(status, style: TextStyle(color: sColor, fontWeight: FontWeight.w700, fontSize: 12))),
              ]),
              const SizedBox(height: 20),

              // Amount card
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(gradient: AppColors.cardGradient, borderRadius: BorderRadius.circular(14)),
                child: Row(mainAxisAlignment: MainAxisAlignment.spaceAround, children: [
                  _detailStat('Weight', '${kg.toStringAsFixed(2)} kg', Colors.white),
                  _detailStat('Rate',   'ETB ${price.toStringAsFixed(2)}', Colors.white70),
                  _detailStat('Total',  'ETB ${total.toStringAsFixed(2)}', Colors.white),
                ])),
              const SizedBox(height: 20),

              _detailRow(Icons.location_on_rounded,    'Wereda',  r['wereda_name']?.toString() ?? '—', isDark),
              _detailRow(Icons.calendar_today_rounded, 'Date',    r['receipt_date']?.toString() ?? '—', isDark),
              if ((r['notes']?.toString() ?? '').isNotEmpty)
                _detailRow(Icons.notes_rounded, 'Notes', r['notes'].toString(), isDark),
            ])),
          ]),
        ),
      ),
    );
  }

  Widget _detailStat(String label, String val, Color color) => Column(children: [
    Text(val, style: TextStyle(color: color, fontWeight: FontWeight.w800, fontSize: 14)),
    const SizedBox(height: 2),
    Text(label, style: TextStyle(color: color.withValues(alpha: 0.7), fontSize: 10)),
  ]);

  Widget _detailRow(IconData icon, String label, String val, bool isDark) => Padding(
    padding: const EdgeInsets.only(bottom: 12),
    child: Row(children: [
      Icon(icon, size: 16, color: AppColors.primary),
      const SizedBox(width: 10),
      SizedBox(width: 70, child: Text(label, style: const TextStyle(color: Colors.grey, fontSize: 12))),
      Expanded(child: Text(val, style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13,
        color: isDark ? Colors.white : AppColors.textPrimary))),
    ]),
  );

  Widget _infoChip(String label, String val, IconData icon, Color color) => Column(children: [
    Icon(icon, color: color, size: 15),
    const SizedBox(height: 3),
    Text(val, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 11, color: color)),
    Text(label, style: const TextStyle(fontSize: 9, color: Colors.grey)),
  ]);

  Widget _sectionHeader(String title, bool isDark) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
    decoration: BoxDecoration(
      color: AppColors.primary.withValues(alpha: 0.08), borderRadius: BorderRadius.circular(10)),
    child: Text(title, style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13,
      color: isDark ? Colors.white : AppColors.primaryDark)),
  );

  Widget _fieldLabel(String t) => Padding(
    padding: const EdgeInsets.only(bottom: 6),
    child: Text(t, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12, color: Colors.grey)),
  );

  Widget _styledDropdown<T>({required String hint, required T value,
      required List<DropdownMenuItem<T>> items, required ValueChanged<T?> onChanged, required bool isDark}) =>
    Container(
      decoration: BoxDecoration(
        color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.grey.shade300)),
      padding: const EdgeInsets.symmetric(horizontal: 12),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<T>(
          isExpanded: true, value: value,
          dropdownColor: isDark ? const Color(0xFF1E2D2C) : Colors.white,
          hint: Text(hint, style: const TextStyle(color: Colors.grey, fontSize: 13)),
          items: items, onChanged: onChanged)));

  Widget _tapField(String val, IconData icon, {required VoidCallback onTap, required bool isDark}) =>
    GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
        decoration: BoxDecoration(
          color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: Colors.grey.shade300)),
        child: Row(children: [
          Icon(icon, size: 16, color: AppColors.primary),
          const SizedBox(width: 8),
          Text(val, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600,
            color: isDark ? Colors.white : AppColors.textPrimary)),
        ])));

  InputDecoration _inputDeco(String hint, IconData icon, bool isDark) => InputDecoration(
    hintText: hint, hintStyle: const TextStyle(color: Colors.grey, fontSize: 13),
    prefixIcon: Icon(icon, size: 18, color: AppColors.primary),
    filled: true, fillColor: isDark ? const Color(0xFF1E2D2C) : Colors.white,
    border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: Colors.grey.shade300)),
    enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide(color: Colors.grey.shade300)),
    focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.primary, width: 1.5)),
    contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14));
}

// ══════════════════════════════════════════════════════════════════════════════
// BOTTOM NAV
// ══════════════════════════════════════════════════════════════════════════════
class _OutsourceBottomNav extends StatelessWidget {
  final int currentIndex;
  final ValueChanged<int> onTap;
  final bool isDark;
  const _OutsourceBottomNav({required this.currentIndex, required this.onTap, required this.isDark});

  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.fromLTRB(16, 0, 16, 16),
    decoration: BoxDecoration(
      color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
      borderRadius: BorderRadius.circular(24),
      boxShadow: [BoxShadow(color: isDark ? Colors.black38 : AppColors.shadow,
        blurRadius: 20, offset: const Offset(0, 8))]),
    child: ClipRRect(
      borderRadius: BorderRadius.circular(24),
      child: NavigationBar(
        height: 64,
        selectedIndex: currentIndex,
        onDestinationSelected: onTap,
        backgroundColor: Colors.transparent,
        indicatorColor: AppColors.primary.withValues(alpha: 0.15),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysHide,
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.business_outlined),
            selectedIcon: Icon(Icons.business_rounded, color: AppColors.primary),
            label: 'Company'),
          NavigationDestination(
            icon: Icon(Icons.person_outline_rounded),
            selectedIcon: Icon(Icons.person_rounded, color: AppColors.primary),
            label: 'Profile'),
        ],
      ),
    ),
  );
}
