using System;
using UnityEngine.Assertions.Must;

// данные ячейки, которые хранят информацию о том,
//что находится в конкретной клетке игрового поля.
public class CellData
{

    public CellType cellType;
    public Point point;
    private Cell _cell;

    //Enum - используется для создания набора именованных констант
    //это способ дать человеческие имена числам, чтобы код было легче читать.

    // Blank (0) Здесь пусто, но сюда можно что-то положить.
    // Hole (-1) означает: Здесь принципиально ничего не может быть
   public enum CellType
    {
        Hole = -1,
        Blank = 0,
        Red = 1,
        Green = 2,
        Blue = 3,
        Yellow = 4,
        Pink = 5,


        Plane = 6,
        Rocket = 7,
        Bomb = 8,
        Discoball = 9

    }

 
   
   
    // конструктор класса, он берет тип и координаты и превращает в готовый объект данных.
    public CellData(CellType cellType, Point point)
    {
        this.cellType = cellType;  
        this.point = point;
        
    }


    //Это доступ в приватное поле. Защищает целостность данных и делает код более предсказуемым.
    public Cell GetCell()
    {
        return _cell;
    }

    // Распределяет кристаллы по клеткам, следит за соответствием данных.
    // Определяет координату каждого типа кристалла
    public void SetCell(Cell newCell)
    {
        _cell = newCell;

        if(_cell == null)
        {
            cellType = CellType.Blank;
        }
        else
        {
            cellType = newCell.CellType;
            _cell.SetCellPoint(point);
        }
    }
}