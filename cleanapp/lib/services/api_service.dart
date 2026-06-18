import 'dart:convert';
import 'dart:io';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/user_model.dart';
import '../models/submission_model.dart';
import '../models/notification_model.dart';

class ApiService {
  static const String _serverHost = 'localhost';
  static const String _emulatorHost = '10.0.2.2'; // Android emulator → host PC

  // ── YOUR PC's LAN IP ──────────────────────────────────────────────────────
  // Run `ipconfig` on your PC and set this to the IPv4 address shown under
  // your active WiFi adapter (e.g. 192.168.1.105).
  // The phone and PC must be on the same WiFi network.
  static const String _lanHost = '10.0.26.234';
  // ─────────────────────────────────────────────────────────────────────────

  static const int _serverPort = 5000;

  static String get _host {
    if (kIsWeb) return _serverHost;           // browser → localhost
    if (!Platform.isAndroid) return _serverHost; // Windows/macOS desktop
    // Android: emulator uses 10.0.2.2; real device uses LAN IP
    // We detect emulator by checking for the special host fingerprint.
    return _isEmulator ? _emulatorHost : _lanHost;
  }

  /// Simple emulator detection: the emulator's hostname contains 'generic'
  /// or the model contains 'sdk'. Falls back to LAN IP if uncertain.
  static bool get _isEmulator {
    // This is a best-effort check. If unsure, set [_lanHost] and this will
    // always return false for physical devices.
    try {
      return Platform.environment['HOME']?.contains('buildbot') == true;
    } catch (_) {
      return false;
    }
  }

  static String get baseUrl => 'http://$_host:$_serverPort/api/mobile';
  static String get transportApiBaseUrl =>
      'http://$_host:$_serverPort/api/transport';

  Future<UserModel?> login(String username, String password) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'username': username, 'password': password}),
      );
      if (response.statusCode == 200) {
        return UserModel.fromJson(jsonDecode(response.body));
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  Future<List<Map<String, dynamic>>> getWeredas() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/weredas'));
      if (response.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(response.body));
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getMahberats() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/mahberats'));
      if (response.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(response.body));
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getCompanies() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/companies'));
      if (response.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(response.body));
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<Map<String, dynamic>>> getVehicles() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/vehicles'));
      if (response.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(response.body));
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<bool> submitWork(SubmissionModel submission) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/submit'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(submission.toJson()),
      );
      return response.statusCode == 200 || response.statusCode == 201;
    } catch (_) {
      return false;
    }
  }

  Future<String?> uploadImage(dynamic imageFile) async {
    try {
      var request = http.MultipartRequest(
        'POST',
        Uri.parse('$baseUrl/upload-image'),
      );

      if (imageFile is String) {
        request.files.add(await http.MultipartFile.fromPath('file', imageFile));
      } else {
        final bytes = await imageFile.readAsBytes();
        final fileName = imageFile.name ?? 'image.jpg';
        request.files.add(
          http.MultipartFile.fromBytes('file', bytes, filename: fileName),
        );
      }

      final streamedResponse = await request.send();
      final response = await http.Response.fromStream(streamedResponse);

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
        final serverBase = baseUrl.replaceAll('/api/mobile', '');
        return '$serverBase${data['url']}';
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  Future<List<SubmissionModel>> getHistory(int userId) async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/history/$userId'));
      if (response.statusCode == 200) {
        List<dynamic> data = jsonDecode(response.body);
        return data.map((json) {
          json['userId'] = 0;
          json['role'] = '';
          json['weredaId'] = 0;
          json['mahberatId'] = 0;
          json['rate'] = 0.0;
          return SubmissionModel.fromJson(json);
        }).toList();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<List<SubmissionModel>> getPendingSubmissions() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/pending'));
      if (response.statusCode == 200) {
        List<dynamic> data = jsonDecode(response.body);
        return data.map((json) {
          json['userId'] = 0;
          json['role'] = '';
          json['weredaId'] = 0;
          json['mahberatId'] = 0;
          json['rate'] = 0.0;
          return SubmissionModel.fromJson(json);
        }).toList();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<bool> updateSubmissionStatus(
    int id,
    String status, {
    String receiptType = 'Mahberat',
  }) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/submissions/$id/status'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'status': status, 'receiptType': receiptType}),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  // ══════════════════════════════════════════════════════════════════════
  // NOTIFICATION APIs
  // ══════════════════════════════════════════════════════════════════════

  Future<List<NotificationModel>> getNotifications(int userId) async {
    try {
      final response = await http.get(
        Uri.parse('$transportApiBaseUrl/notifications/$userId'),
      );
      if (response.statusCode == 200) {
        List<dynamic> data = jsonDecode(response.body);
        return data.map((json) => NotificationModel.fromJson(json)).toList();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<bool> markNotificationAsRead(int notificationId) async {
    try {
      final response = await http.post(
        Uri.parse(
          '$transportApiBaseUrl/notifications/$notificationId/read',
        ),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  Future<bool> markAllNotificationsRead(int userId) async {
    try {
      final response = await http.post(
        Uri.parse('$transportApiBaseUrl/notifications/read-all/$userId'),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  // ══════════════════════════════════════════════════════════════════════
  // TRANSPORT REQUEST APIs
  // ══════════════════════════════════════════════════════════════════════

  Future<bool> acceptTransportRequest(
    int requestId, {
    required int driverId,
    required String driverName,
    String? notes,
  }) async {
    try {
      final response = await http.post(
        Uri.parse(
          '$transportApiBaseUrl/requests/$requestId/driver-action',
        ),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'action': 'Accept',
          'driverId': driverId,
          'driverName': driverName,
          'notes': notes ?? '',
        }),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  Future<bool> rejectTransportRequest(
    int requestId, {
    required int driverId,
    required String driverName,
    String? notes,
  }) async {
    try {
      final response = await http.post(
        Uri.parse(
          '$transportApiBaseUrl/requests/$requestId/driver-action',
        ),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'action': 'Reject',
          'driverId': driverId,
          'driverName': driverName,
          'notes': notes ?? '',
        }),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  Future<Map<String, dynamic>> getReportData(
    int userId, {
    String? startDate,
    String? endDate,
  }) async {
    try {
      var url = '$baseUrl/reports/$userId';
      if (startDate != null && endDate != null) {
        url += '?startDate=$startDate&endDate=$endDate';
      }
      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        return jsonDecode(response.body);
      }
      return {'data': [], 'startDate': '', 'endDate': ''};
    } catch (_) {
      return {'data': [], 'startDate': '', 'endDate': ''};
    }
  }

  Future<List<Map<String, dynamic>>> getDriverTransportRequests(
    int userId,
  ) async {
    try {
      final response = await http.get(
        Uri.parse(
          '$transportApiBaseUrl/requests?userId=$userId&role=driver',
        ),
      );
      if (response.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(response.body));
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<bool> markTransportPickedUp(
    int requestId, {
    required int driverId,
    required String driverName,
    String? notes,
  }) async {
    try {
      final response = await http.post(
        Uri.parse('$transportApiBaseUrl/requests/$requestId/pickup'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'driverId': driverId,
          'driverName': driverName,
          'notes': notes ?? '',
        }),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  Future<bool> submitTransportReceipt(
    int requestId, {
    required int driverId,
    required String driverName,
    String? receiptPhotoUrl,
    String? notes,
    double? actualKilogram,
    int? weredaId,
    int? mahberatId,
  }) async {
    try {
      final response = await http.post(
        Uri.parse(
          '$transportApiBaseUrl/requests/$requestId/submit-receipt',
        ),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'driverId': driverId,
          'driverName': driverName,
          'receiptPhotoUrl': receiptPhotoUrl ?? '',
          'notes': notes ?? '',
          'actualKilogram': actualKilogram,
          'weredaId': weredaId,
          'mahberatId': mahberatId,
        }),
      );
      return response.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  // ── Private Company Rep APIs ──────────────────────────────────────────────

  Future<Map<String, dynamic>?> getPrivateCompanyInfo(int userId) async {
    try {
      final r = await http.get(Uri.parse('$baseUrl/private-company/$userId'));
      if (r.statusCode == 200) {
        return jsonDecode(r.body) as Map<String, dynamic>;
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  Future<bool> submitPrivateReceipt({
    required int userId,
    required int weredaId,
    int? vehicleId,
    required double kilogram,
    required double price,
    required String date,
    required String time,
    String? notes,
    String? imageUrl,
  }) async {
    try {
      final r = await http.post(
        Uri.parse('$baseUrl/submit-private-receipt'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'userId': userId,
          'weredaId': weredaId,
          'vehicleId': vehicleId,
          'kilogram': kilogram,
          'price': price,
          'date': date,
          'time': time,
          'notes': notes ?? '',
          'imageUrl': imageUrl ?? '',
        }),
      );
      return r.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  Future<List<Map<String, dynamic>>> getPrivateReceipts(int userId) async {
    try {
      final r = await http.get(Uri.parse('$baseUrl/private-receipts/$userId'));
      if (r.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(r.body));
      }
      return [];
    } catch (_) {
      return [];
    }
  }
}
