import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LanguageProvider extends ChangeNotifier {
  String _lang = 'en'; // 'en' or 'am'

  String get lang => _lang;
  bool get isAmharic => _lang == 'am';

  LanguageProvider() {
    _load();
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    _lang = prefs.getString('lang') ?? 'en';
    notifyListeners();
  }

  Future<void> setLanguage(String lang) async {
    _lang = lang;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('lang', lang);
    notifyListeners();
  }

  // Translation map
  static const Map<String, Map<String, String>> _t = {
    'en': {
      'settings':          'Settings',
      'edit_profile':      'Edit Profile',
      'change_password':   'Change Password',
      'language':          'Language',
      'logout':            'Logout',
      'name':              'Full Name',
      'phone':             'Phone Number',
      'save':              'Save Changes',
      'old_password':      'Current Password',
      'new_password':      'New Password',
      'confirm_password':  'Confirm New Password',
      'update':            'Update Password',
      'english':           'English',
      'amharic':           'Amharic (????)',
      'app_version':       'App Version',
      'profile':           'Profile',
      'account':           'Account',
      'appearance':        'Appearance',
      'success':           'Success',
      'error':             'Error',
      'fill_all':          'Please fill in all fields.',
      'pass_no_match':     'New passwords do not match.',
      'pass_short':        'Password must be at least 6 characters.',
      'profile_updated':   'Profile updated successfully.',
      'password_updated':  'Password changed successfully.',
    },
    'am': {
      'settings':          '?????',
      'edit_profile':      '??? ???',
      'change_password':   '???? ?? ???',
      'language':          '???',
      'logout':            '??',
      'name':              '?? ??',
      'phone':             '??? ???',
      'save':              '????? ?????',
      'old_password':      '???? ???? ??',
      'new_password':      '??? ???? ??',
      'confirm_password':  '??? ???? ??? ?????',
      'update':            '???? ??? ????',
      'english':           'English',
      'amharic':           '????',
      'app_version':       '??? ???',
      'profile':           '????',
      'account':           '???',
      'appearance':        '???',
      'success':           '???',
      'error':             '????',
      'fill_all':          '???? ???? ???? ????',
      'pass_no_match':     '???? ???? ??? ???????',
      'pass_short':        '???? ?? ???? 6 ????? ??? ?????',
      'profile_updated':   '???? ???? ??? ?????',
      'password_updated':  '???? ?? ???? ??? ??????',
    },
  };

  String tr(String key) => _t[_lang]?[key] ?? _t['en']?[key] ?? key;
}
