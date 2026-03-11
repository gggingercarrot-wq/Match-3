using DG.Tweening;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlaneBooster
{
    private readonly BoardService _boardService;
    private readonly CellFactory _cellFactory;
    private readonly ScoreService _scoreService;


    //массив для проверки соседних клеток
    private readonly Point[] _neighborDirections = new Point[]
    {
        Point.up,
        Point.down,
        Point.left,
        Point.right
    };

    public PlaneBooster(BoardService boardService, CellFactory cellFactory, ScoreService scoreService)
    {
        _boardService = boardService;
        _cellFactory = cellFactory;
        _scoreService = scoreService;
    }


    // Активирует специальный элемент самолетик, который
    public void ActivatePlane(Point planePoint, CellData.CellType swappedCellType)
    {
        var cellAtPoint = _boardService.GetCellAtPoint(planePoint);
        if (cellAtPoint == null) return;

        var planeCell = cellAtPoint.GetCell();
        if (planeCell == null) return;

        // ШАГ 1: Локальный взрыв (самолетик + 4 соседа)
        List<Point> localExplosionPoints = GetLocalExplosionPoints(planePoint);

        // ШАГ 2: Поиск цели того же типа, что и свапнутая ячейка ???
        Point targetPoint = FindTargetOfType(swappedCellType, planePoint);

        // Объединяем все точки для удаления
        List<Point> allPointsToRemove = new List<Point>(localExplosionPoints);
        if (targetPoint != null)
        {
            allPointsToRemove.Add(targetPoint);
            Debug.Log($"Самолетик летит в точку ({targetPoint.x}, {targetPoint.y}) с типом {swappedCellType}");
        }

        // Запускаем анимацию и удаление
        StartPlaneSequence(planeCell, allPointsToRemove, planePoint, targetPoint);
    }

    private List<Point> GetLocalExplosionPoints(Point centerPoint)
    {
        List<Point> points = new List<Point>();
        points.Add(centerPoint); // сам самолетик

        // Добавляем 4 соседние ячейки
        foreach (var direction in _neighborDirections)
        {
            Point neighborPoint = Point.Add(centerPoint, direction);

            var cellType = _boardService.GetCellTypeAtPoint(neighborPoint);
            if (cellType != CellData.CellType.Hole)
            {
                points.Add(neighborPoint);
            }
        }

        return points;
    }

    // полет самолетика выбор цели
    private Point FindTargetOfType(CellData.CellType targetType, Point planePoint)
    {
        if (targetType <= 0 || targetType == CellData.CellType.Hole || targetType == CellData.CellType.Plane)
        {
            return null;
        }

        // Ищем ближайшую цель
        Point closestTarget = null;
        float minDistance = float.MaxValue;

        for (int x = 0; x < StaticData.Config.BoardWidth; x++)
        {
            for (int y = 0; y < StaticData.Config.BoardHeight; y++)
            {
                Point point = new Point(x, y);

                if (point.Equals(planePoint)) continue;

                var cellType = _boardService.GetCellTypeAtPoint(point);

                if (cellType == targetType)
                {
                    float distance = Mathf.Abs(x - planePoint.x) + Mathf.Abs(y - planePoint.y);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestTarget = point;
                    }
                }
            }
        }

        return closestTarget;
    }

    private void StartPlaneSequence(Cell planeCell, List<Point> pointsToRemove, Point planePoint, Point targetPoint)
    {
        if (pointsToRemove.Count == 0) return;

        Vector2 startPos = planeCell.rect.anchoredPosition;
        DG.Tweening.Sequence sequence = DOTween.Sequence();

        // Шаг 1: Подпрыгивание
        sequence.Append(planeCell.rect.DOAnchorPosY(startPos.y + 30f, 0.15f).SetEase(Ease.OutQuad));
        sequence.Join(planeCell.transform.DOScale(1.2f, 0.15f));

        // Шаг 2: Возврат
        sequence.Append(planeCell.rect.DOAnchorPosY(startPos.y, 0.1f).SetEase(Ease.InQuad));

        if (targetPoint != null)
        {
            // Шаг 3: Полет к цели
            Vector2 targetPos = BoardService.GetBoardPositionFromPoint(targetPoint);
            sequence.Append(planeCell.rect.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.InQuad));
            sequence.Join(planeCell.transform.DOScale(0.8f, 0.4f));
        }

        // Шаг 4: Взрыв
        sequence.AppendCallback(() =>
        {
            ExplodePoints(pointsToRemove, planePoint);
        });

        // Шаг 5: Возврат масштаба
        sequence.Append(planeCell.transform.DOScale(1f, 0.1f));

        sequence.Play();
    }

    private void ExplodePoints(List<Point> pointsToRemove, Point planePoint)
    {
        List<Cell> cellsToRemove = new List<Cell>();
        List<Point> validPoints = new List<Point>();

        foreach (var point in pointsToRemove)
        {
            var cellData = _boardService.GetCellAtPoint(point);
            if (cellData != null)
            {
                var cell = cellData.GetCell();
                if (cell != null && cell.gameObject.activeSelf)
                {
                    cellsToRemove.Add(cell);
                    validPoints.Add(point);
                    _cellFactory.KillCell(point);
                }
            }
        }

        if (validPoints.Count == 0) return;

        // Начисляем очки
        _scoreService.AddScore(validPoints.Count * 10);

        // Анимируем исчезновение всех ячеек
        foreach (var cell in cellsToRemove)
        {
            if (cell != null && cell.gameObject != null)
            {
                cell.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 5, 0.5f).OnComplete(() =>
                {
                    if (cell != null && cell.gameObject != null)
                    {
                        cell.gameObject.SetActive(false);
                    }
                });
            }
        }

        // Очищаем данные ячеек
        foreach (var point in validPoints)
        {
            var cellData = _boardService.GetCellAtPoint(point);
            if (cellData != null)
            {
                cellData.SetCell(null);
            }
        }

        _boardService.Invoke("ApplyGravityToBoard", 0.3f);
    }
}