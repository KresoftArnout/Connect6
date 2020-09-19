window.soundPlayer = {
  playStoneSound: function () {
    document.getElementById('stoneSound').load();
    document.getElementById('stoneSound').volume = 0.5;
    document.getElementById('stoneSound').play();
  }
}

