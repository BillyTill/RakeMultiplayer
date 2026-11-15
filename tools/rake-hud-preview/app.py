"""Rake HUD preview program (PyQt5).

Keys:
    H  cycle health          C  toggle cooldowns      D  damage burst
    N  day / night           M  sense markers        B  background
    S  save PNG              F  fullscreen           Esc  quit
"""

import os
import sys

from PIL import Image
from PyQt5.QtCore import Qt, QTimer
from PyQt5.QtGui import QImage, QPixmap
from PyQt5.QtWidgets import QApplication, QLabel, QMainWindow

import hud
import preview

HERE = os.path.dirname(os.path.abspath(__file__))
BACKGROUNDS = ["night-camp.jpg", "forest-day.jpg", "river-day.jpg"]


class Window(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Rake HUD — preview")
        self.resize(1600, 900)

        self.label = QLabel(self)
        self.label.setAlignment(Qt.AlignCenter)
        self.label.setStyleSheet("background:#050706;")
        self.setCentralWidget(self.label)

        self.state = preview.base_state()
        self.bg_index = 0
        self.bg = self._load_bg()
        self.t = 0.0
        self.damage = 0.0
        self.cooldowns_on = True
        self.markers_on = True
        self._buffer = None

        self.statusBar().showMessage(
            "H  health   C  cooldowns   D  damage   N  day/night   M  markers   "
            "B  background   S  save   F  fullscreen   Esc  quit")

        self.timer = QTimer(self)
        self.timer.timeout.connect(self.tick)
        self.timer.start(40)

        self._render()

    def _load_bg(self):
        return hud.prepare_background(os.path.join(HERE, "assets", BACKGROUNDS[self.bg_index]))

    def _render(self):
        frame = hud.compose(self.state, self.bg, self.t).convert("RGBA")
        self._buffer = frame.tobytes()
        img = QImage(self._buffer, hud.CW, hud.CH, hud.CW * 4, QImage.Format_RGBA8888)
        pix = QPixmap.fromImage(img)
        self.label.setPixmap(pix.scaled(self.label.size(), Qt.KeepAspectRatio, Qt.FastTransformation))

    def tick(self):
        self.t += 0.04
        self.state["time_left"] = max(0.0, self.state["time_left"] - 0.02)
        if self.damage > 0:
            self.damage = max(0.0, self.damage - 0.035)
            self.state["damage"] = self.damage
        self._render()

    def keyPressEvent(self, e):
        key = e.key()

        if key == Qt.Key_H:
            steps = [2500, 1850, 900, 320]
            self.state["hp"] = steps[(steps.index(self.state["hp"]) + 1) % len(steps)] \
                if self.state["hp"] in steps else steps[0]
        elif key == Qt.Key_C:
            self.cooldowns_on = not self.cooldowns_on
            if self.cooldowns_on:
                self.state["abilities"] = [("GRAB", 14, 20), ("ANIMAL", 0, 10),
                                           ("CAMERA", 33, 50), ("LEAP", 0, 50)]
            else:
                self.state["abilities"] = [("GRAB", 0, 20), ("ANIMAL", 0, 10),
                                           ("CAMERA", 0, 50), ("LEAP", 0, 50)]
        elif key == Qt.Key_D:
            self.damage = 1.0
            self.state["damage"] = 1.0
        elif key == Qt.Key_N:
            self.state["night"] = not self.state["night"]
            self.bg_index = 0 if self.state["night"] else 1
            self.bg = self._load_bg()
        elif key == Qt.Key_M:
            self.markers_on = not self.markers_on
            self.state["markers"] = preview.base_state()["markers"] if self.markers_on else []
        elif key == Qt.Key_B:
            self.bg_index = (self.bg_index + 1) % len(BACKGROUNDS)
            self.bg = self._load_bg()
        elif key == Qt.Key_S:
            self._save()
        elif key == Qt.Key_F:
            self.showNormal() if self.isFullScreen() else self.showFullScreen()
        elif key == Qt.Key_Escape:
            self.close()
        else:
            super().keyPressEvent(e)

    def _save(self):
        frame = hud.compose(self.state, self.bg, self.t).convert("RGB")
        frame = frame.resize((hud.W, hud.H), Image.NEAREST)
        path = os.path.join(HERE, "rake-hud.png")
        frame.save(path)
        self.statusBar().showMessage("saved: " + path, 4000)


def main():
    app = QApplication(sys.argv)
    w = Window()
    w.show()
    sys.exit(app.exec_())


if __name__ == "__main__":
    main()
