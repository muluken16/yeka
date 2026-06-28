import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../theme/app_colors.dart';
import '../services/auth_service.dart';
import '../providers/language_provider.dart';
import '../services/api_service.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});
  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  final _api = ApiService();

  // ── Change Password state ─────────────────────────────────────────────────
  final _oldPassCtrl     = TextEditingController();
  final _newPassCtrl     = TextEditingController();
  final _confirmPassCtrl = TextEditingController();
  bool _obscureOld     = true;
  bool _obscureNew     = true;
  bool _obscureConfirm = true;
  bool _changingPass   = false;

  // ── Edit Profile state ────────────────────────────────────────────────────
  late TextEditingController _phoneCtrl;
  bool _updatingProfile = false;

  @override
  void initState() {
    super.initState();
    final user = context.read<AuthService>().currentUser;
    _phoneCtrl = TextEditingController(text: user?.phone ?? '');
  }

  @override
  void dispose() {
    _oldPassCtrl.dispose();
    _newPassCtrl.dispose();
    _confirmPassCtrl.dispose();
    _phoneCtrl.dispose();
    super.dispose();
  }

  void _snack(String msg, {bool error = false}) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(msg),
      backgroundColor: error ? Colors.red : AppColors.primary,
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ));
  }

  Future<void> _changePassword() async {
    final lp   = context.read<LanguageProvider>();
    final user = context.read<AuthService>().currentUser;
    final old  = _oldPassCtrl.text.trim();
    final nw   = _newPassCtrl.text.trim();
    final conf = _confirmPassCtrl.text.trim();

    if (old.isEmpty || nw.isEmpty || conf.isEmpty) { _snack(lp.tr('fill_all'), error: true); return; }
    if (nw != conf)                                 { _snack(lp.tr('pass_no_match'), error: true); return; }
    if (nw.length < 6)                              { _snack(lp.tr('pass_short'), error: true); return; }

    setState(() => _changingPass = true);
    final res = await _api.changePassword(userId: user!.id, oldPassword: old, newPassword: nw);
    setState(() => _changingPass = false);

    if (res['success'] == true) {
      _oldPassCtrl.clear(); _newPassCtrl.clear(); _confirmPassCtrl.clear();
      _snack(lp.tr('password_updated'));
    } else {
      _snack(res['message'] ?? lp.tr('error'), error: true);
    }
  }

  Future<void> _updateProfile() async {
    final lp   = context.read<LanguageProvider>();
    final user = context.read<AuthService>().currentUser;
    final ph   = _phoneCtrl.text.trim();
    if (ph.isEmpty) { _snack(lp.tr('fill_all'), error: true); return; }

    setState(() => _updatingProfile = true);
    final res = await _api.updateProfile(userId: user!.id, phone: ph);
    setState(() => _updatingProfile = false);

    if (res['success'] == true) {
      _snack(lp.tr('profile_updated'));
    } else {
      _snack(res['message'] ?? lp.tr('error'), error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final lp     = context.watch<LanguageProvider>();
    final auth   = context.watch<AuthService>();
    final user   = auth.currentUser;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      backgroundColor: isDark ? const Color(0xFF0D1B1A) : const Color(0xFFF4F6FB),
      appBar: AppBar(
        backgroundColor: AppColors.primary,
        foregroundColor: Colors.white,
        title: Text(lp.tr('settings'), style: const TextStyle(fontWeight: FontWeight.w800)),
        elevation: 0,
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 20, 16, 40),
        children: [

          // ── User header card ───────────────────────────────────────────────
          Container(
            padding: const EdgeInsets.all(20),
            margin: const EdgeInsets.only(bottom: 20),
            decoration: BoxDecoration(
              gradient: AppColors.cardGradient,
              borderRadius: BorderRadius.circular(20),
              boxShadow: [BoxShadow(color: AppColors.primary.withValues(alpha: 0.3), blurRadius: 12, offset: const Offset(0, 4))],
            ),
            child: Row(children: [
              CircleAvatar(
                radius: 32,
                backgroundColor: Colors.white.withValues(alpha: 0.25),
                child: Text(
                  (user?.name.isNotEmpty == true) ? user!.name[0].toUpperCase() : 'U',
                  style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w900),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(user?.name ?? '—', style: const TextStyle(color: Colors.white, fontSize: 17, fontWeight: FontWeight.w800)),
                const SizedBox(height: 4),
                Text(user?.role.toUpperCase() ?? '', style: TextStyle(color: Colors.white.withValues(alpha: 0.7), fontSize: 12, fontWeight: FontWeight.w600)),
                if ((user?.phone ?? '').isNotEmpty)
                  Text(user!.phone, style: TextStyle(color: Colors.white.withValues(alpha: 0.7), fontSize: 12)),
              ])),
            ]),
          ),

          // ── Edit Profile section ──────────────────────────────────────────
          _sectionTitle(Icons.person_rounded, lp.tr('edit_profile'), isDark),
          _card(isDark, children: [
            _fieldLabel(lp.tr('phone'), isDark),
            TextField(
              controller: _phoneCtrl,
              keyboardType: TextInputType.phone,
              decoration: _inputDeco(lp.tr('phone'), Icons.phone_rounded),
              style: TextStyle(color: isDark ? Colors.white : AppColors.textPrimary),
            ),
            const SizedBox(height: 14),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: _updatingProfile ? null : _updateProfile,
                icon: _updatingProfile
                    ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Icon(Icons.save_rounded),
                label: Text(lp.tr('save')),
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
              ),
            ),
          ]),

          const SizedBox(height: 20),

          // ── Change Password section ───────────────────────────────────────
          _sectionTitle(Icons.lock_rounded, lp.tr('change_password'), isDark),
          _card(isDark, children: [
            _passField(lp.tr('old_password'), _oldPassCtrl, _obscureOld, () => setState(() => _obscureOld = !_obscureOld), isDark),
            const SizedBox(height: 12),
            _passField(lp.tr('new_password'), _newPassCtrl, _obscureNew, () => setState(() => _obscureNew = !_obscureNew), isDark),
            const SizedBox(height: 12),
            _passField(lp.tr('confirm_password'), _confirmPassCtrl, _obscureConfirm, () => setState(() => _obscureConfirm = !_obscureConfirm), isDark),
            const SizedBox(height: 14),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                onPressed: _changingPass ? null : _changePassword,
                icon: _changingPass
                    ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Icon(Icons.key_rounded),
                label: Text(lp.tr('update')),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF059669),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
              ),
            ),
          ]),

          const SizedBox(height: 20),

          // ── Language section ──────────────────────────────────────────────
          _sectionTitle(Icons.language_rounded, lp.tr('language'), isDark),
          _card(isDark, children: [
            _langTile('English', 'en', lp, isDark),
            const Divider(height: 1),
            _langTile('አማርኛ (Amharic)', 'am', lp, isDark),
          ]),

          const SizedBox(height: 20),

          // ── Logout ────────────────────────────────────────────────────────
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              onPressed: () {
                context.read<AuthService>().logout();
                context.go('/login');
              },
              icon: const Icon(Icons.logout_rounded, color: Colors.red),
              label: Text(lp.tr('logout'), style: const TextStyle(color: Colors.red, fontWeight: FontWeight.w700)),
              style: OutlinedButton.styleFrom(
                side: const BorderSide(color: Colors.red),
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),
          ),

          const SizedBox(height: 30),
          Center(
            child: Text('Yeka Cleaning v1.0.0', style: TextStyle(color: isDark ? Colors.white38 : Colors.black26, fontSize: 12)),
          ),
        ],
      ),
    );
  }

  Widget _sectionTitle(IconData icon, String title, bool isDark) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Row(children: [
      Icon(icon, size: 18, color: AppColors.primary),
      const SizedBox(width: 8),
      Text(title, style: TextStyle(
        fontWeight: FontWeight.w800, fontSize: 14,
        color: isDark ? Colors.white : const Color(0xFF1E293B),
      )),
    ]),
  );

  Widget _card(bool isDark, {required List<Widget> children}) => Container(
    padding: const EdgeInsets.all(16),
    margin: const EdgeInsets.only(bottom: 4),
    decoration: BoxDecoration(
      color: isDark ? const Color(0xFF1E2D2C) : Colors.white,
      borderRadius: BorderRadius.circular(16),
      boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.06), blurRadius: 8)],
    ),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: children),
  );

  Widget _fieldLabel(String label, bool isDark) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(label, style: TextStyle(
      fontWeight: FontWeight.w700, fontSize: 13,
      color: isDark ? Colors.white70 : const Color(0xFF374151),
    )),
  );

  Widget _passField(String label, TextEditingController ctrl, bool obscure, VoidCallback toggle, bool isDark) => TextField(
    controller: ctrl,
    obscureText: obscure,
    style: TextStyle(color: isDark ? Colors.white : AppColors.textPrimary),
    decoration: _inputDeco(label, Icons.lock_outline_rounded).copyWith(
      suffixIcon: IconButton(
        icon: Icon(obscure ? Icons.visibility_off_rounded : Icons.visibility_rounded, color: AppColors.textHint),
        onPressed: toggle,
      ),
    ),
  );

  InputDecoration _inputDeco(String label, IconData icon) => InputDecoration(
    labelText: label,
    labelStyle: const TextStyle(color: AppColors.textHint),
    prefixIcon: Icon(icon, color: AppColors.primary, size: 20),
    filled: true,
    fillColor: Colors.grey.withValues(alpha: 0.08),
    border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
    focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: const BorderSide(color: AppColors.primary, width: 2)),
    contentPadding: const EdgeInsets.symmetric(vertical: 14, horizontal: 14),
  );

  Widget _langTile(String label, String code, LanguageProvider lp, bool isDark) => ListTile(
    contentPadding: EdgeInsets.zero,
    leading: Icon(
      code == 'en' ? Icons.flag_rounded : Icons.translate_rounded,
      color: lp.lang == code ? AppColors.primary : AppColors.textHint,
    ),
    title: Text(label, style: TextStyle(
      fontWeight: lp.lang == code ? FontWeight.w800 : FontWeight.w500,
      color: lp.lang == code ? AppColors.primary : (isDark ? Colors.white : AppColors.textPrimary),
    )),
    trailing: lp.lang == code
        ? const Icon(Icons.check_circle_rounded, color: AppColors.primary)
        : null,
    onTap: () => lp.setLanguage(code),
  );
}
