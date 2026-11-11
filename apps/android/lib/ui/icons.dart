import 'package:flutter/material.dart';

/// Portable icon names used in workspace files, mapped to Material icons.
const _icons = <String, IconData>{
  'code': Icons.code,
  'terminal': Icons.terminal,
  'folder': Icons.folder_outlined,
  'globe': Icons.language,
  'camera': Icons.photo_camera_outlined,
  'play_pause': Icons.play_arrow_rounded,
  'skip_next': Icons.skip_next_rounded,
  'skip_previous': Icons.skip_previous_rounded,
  'volume': Icons.volume_up_outlined,
  'mute': Icons.volume_off_outlined,
  'lock': Icons.lock_outline,
  'file': Icons.description_outlined,
  'calculator': Icons.calculate_outlined,
  'note': Icons.edit_note,
  'cpu': Icons.memory,
  'memory': Icons.sd_storage_outlined,
  'mic': Icons.mic_none,
  'star': Icons.star_outline,
  'briefcase': Icons.work_outline,
  'music': Icons.music_note_outlined,
  'download': Icons.download_outlined,
  'link': Icons.link,
  'settings': Icons.settings_outlined,
  'keyboard': Icons.keyboard_outlined,
  'home': Icons.home_outlined,
};

IconData iconFor(String name) => _icons[name] ?? Icons.star_outline;
