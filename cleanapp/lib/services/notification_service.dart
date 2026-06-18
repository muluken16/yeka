import 'package:flutter/material.dart';
import 'dart:async';
import '../models/notification_model.dart';
import 'api_service.dart';

class NotificationService extends ChangeNotifier {
  final ApiService _apiService = ApiService();

  List<NotificationModel> _notifications = [];
  bool _isLoading = false;
  Timer? _pollingTimer;

  List<NotificationModel> get notifications => _notifications;
  bool get isLoading => _isLoading;
  int get unreadCount => _notifications.where((n) => !n.isRead).length;

  void startPolling(int userId, {Duration interval = const Duration(seconds: 15)}) {
    _pollingTimer?.cancel();
    fetchNotifications(userId);
    _pollingTimer = Timer.periodic(interval, (_) => fetchNotifications(userId));
  }

  void stopPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = null;
  }

  Future<void> fetchNotifications(int userId) async {
    try {
      _isLoading = true;
      notifyListeners();

      final newNotifications = await _apiService.getNotifications(userId);
      _notifications = newNotifications;

      _isLoading = false;
      notifyListeners();
    } catch (_) {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> markAsRead(NotificationModel notification) async {
    try {
      final success = await _apiService.markNotificationAsRead(notification.id);
      if (success) {
        final index = _notifications.indexOf(notification);
        if (index >= 0) {
          _notifications[index] = notification.copyWith(isRead: true);
          notifyListeners();
        }
      }
    } catch (_) {}
  }

  Future<void> markAllAsRead(int userId) async {
    try {
      await _apiService.markAllNotificationsRead(userId);
      _notifications = _notifications
          .map((n) => n.copyWith(isRead: true))
          .toList();
      notifyListeners();
    } catch (_) {}
  }

  void clearNotifications() {
    _notifications.clear();
    notifyListeners();
  }

  @override
  void dispose() {
    stopPolling();
    super.dispose();
  }
}
